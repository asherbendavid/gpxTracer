Imports System.IO
Imports Microsoft.Web.WebView2.WinForms

Public Class TracerForm
    Inherits System.Windows.Forms.Form

    ' How often the playback timer ticks and pushes a position update to the map.
    ' Kept as a const so it's easy to tighten up later if more precision is needed.
    Private Const UPDATE_INTERVAL_MS As Integer = 1000

    ' UI controls
    Private WithEvents btnLoadGpx As New Button()
    Private WithEvents btnPlayStop As New Button()
    Private trackPlayback As New TrackBar()
    Private statusStrip1 As New StatusStrip()
    Private lblFileName As New ToolStripStatusLabel()
    Private webView As New WebView2()
    Private WithEvents playbackTimer As New Timer()

    ' Playback / route state (populated once GPX parsing is added)
    Private currentGpxPath As String = String.Empty
    Private isPlaying As Boolean = False
    Private currentRoute As List(Of GpxPoint) = Nothing

    Private Enum PlaybackState
        Idle
        CountingDown
        Playing
    End Enum

    Private playbackStatus As PlaybackState = PlaybackState.Idle
    Private countdownSecondsRemaining As Integer
    Private WithEvents countdownTimer As New Timer()

    Private playbackStartTime As DateTime ' wall-clock UTC, set when actual playback begins
    Private routeStartTime As DateTime    ' first GPX point's timestamp
    Private routeEndTime As DateTime      ' last GPX point's timestamp
    Private currentSegmentIndex As Integer = 0

    Public Sub New()
        InitializeComponent()
    End Sub

    Private Sub InitializeComponent()
        Me.Text = "GPX Route Player"
        Me.WindowState = FormWindowState.Maximized
        Me.MinimumSize = New Size(600, 400)

        ' --- Toolbar panel (top strip) ---
        Dim toolPanel As New Panel()
        toolPanel.Dock = DockStyle.Top
        toolPanel.Height = 40
        toolPanel.Padding = New Padding(6)

        btnLoadGpx.Text = "Load GPX"
        btnLoadGpx.AutoSize = True
        btnLoadGpx.Location = New Point(6, 6)

        btnPlayStop.Text = "Play"
        btnPlayStop.AutoSize = True
        btnPlayStop.Enabled = False ' disabled until a file is loaded
        btnPlayStop.Location = New Point(btnLoadGpx.Right + 8, 6)

        ' Slider is informational only — advances with playback, not user-editable.
        trackPlayback.Location = New Point(btnPlayStop.Right + 12, 4)
        trackPlayback.Width = 400 ' anchored below; will stretch to fill remaining width
        trackPlayback.Minimum = 0
        trackPlayback.Maximum = 1000 ' placeholder range, will be set per-route later
        trackPlayback.TickStyle = TickStyle.None
        trackPlayback.Enabled = False ' display-only, per current design

        toolPanel.Controls.Add(btnLoadGpx)
        toolPanel.Controls.Add(btnPlayStop)
        toolPanel.Controls.Add(trackPlayback)

        ' Stretch the slider to fill remaining toolbar width, resizing with the form.
        AddHandler toolPanel.Resize, Sub()
                                         trackPlayback.Width = Math.Max(50, toolPanel.Width - trackPlayback.Left - 10)
                                     End Sub
        trackPlayback.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right

        ' --- Status bar (bottom) ---
        lblFileName.Text = "No file loaded"
        lblFileName.Spring = True
        lblFileName.TextAlign = ContentAlignment.MiddleLeft
        statusStrip1.Items.Add(lblFileName)
        statusStrip1.Dock = DockStyle.Bottom

        ' --- Map area (fills remaining space) ---
        webView.Dock = DockStyle.Fill

        ' --- Assemble form ---
        Me.Controls.Add(webView)
        Me.Controls.Add(toolPanel)
        Me.Controls.Add(statusStrip1)

        ' --- Playback timer ---
        playbackTimer.Interval = UPDATE_INTERVAL_MS
        playbackTimer.Enabled = False
        countdownTimer.Interval = 1000

        AddHandler Me.Load, AddressOf MainForm_Load
    End Sub

    Private Async Sub MainForm_Load(sender As Object, e As EventArgs)
        ' WebView2 must be initialized before any navigation or script execution.
        Await webView.EnsureCoreWebView2Async()
        ' Navigate to the local Leaflet HTML file once it's added to the project.
        Dim htmlPath As String = Path.Combine(Application.StartupPath, "map.html")
        webView.CoreWebView2.Navigate(New Uri(htmlPath).AbsoluteUri)
    End Sub

    Private Async Sub btnLoadGpx_Click(sender As Object, e As EventArgs) Handles btnLoadGpx.Click
        Using ofd As New OpenFileDialog()
            ofd.Filter = "GPX files (*.gpx)|*.gpx"
            ofd.Title = "Load GPX Route"

            If Not String.IsNullOrEmpty(My.Settings.LastGpxFolder) AndAlso Directory.Exists(My.Settings.LastGpxFolder) Then
                ofd.InitialDirectory = My.Settings.LastGpxFolder
            End If

            If ofd.ShowDialog() = DialogResult.OK Then
                Dim errorMessage As String = String.Empty
                Dim parsedPoints = GpxParser.TryParseGpx(ofd.FileName, errorMessage)

                If parsedPoints Is Nothing Then
                    MessageBox.Show(errorMessage, "Invalid GPX File", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                    ' Previous route (if any) stays loaded — currentRoute is untouched.
                    Return
                End If

                ' Successful load — replace the current route.
                currentRoute = parsedPoints
                currentGpxPath = ofd.FileName
                lblFileName.Text = Path.GetFileName(currentGpxPath)
                btnPlayStop.Enabled = True

                My.Settings.LastGpxFolder = Path.GetDirectoryName(currentGpxPath)
                My.Settings.Save()

                Await PushRouteToMap(currentRoute)
            End If
        End Using
    End Sub

    Private Async Function PushRouteToMap(points As List(Of GpxPoint)) As Task
        ' Build a JS array literal: [[lat,lon],[lat,lon],...]
        Dim sb As New Text.StringBuilder("[")
        For i As Integer = 0 To points.Count - 1
            If i > 0 Then sb.Append(",")
            sb.Append("[").Append(points(i).Lat.ToString(Globalization.CultureInfo.InvariantCulture)).
           Append(",").Append(points(i).Lon.ToString(Globalization.CultureInfo.InvariantCulture)).Append("]")
        Next
        sb.Append("]")

        Dim script As String = $"setRoute({sb.ToString()});"
        Await webView.ExecuteScriptAsync(script)
    End Function

    Private Sub btnPlayStop_Click(sender As Object, e As EventArgs) Handles btnPlayStop.Click
        Select Case playbackStatus
            Case PlaybackState.Idle
                ' Begin the 5-second countdown before actual playback starts.
                playbackStatus = PlaybackState.CountingDown
                countdownSecondsRemaining = 5
                btnPlayStop.Text = "Stop"
                lblFileName.Text = $"Starting in {countdownSecondsRemaining}s..."
                countdownTimer.Start()

            Case PlaybackState.CountingDown
                ' Cancel before playback ever began.
                countdownTimer.Stop()
                playbackStatus = PlaybackState.Idle
                btnPlayStop.Text = "Play"
                lblFileName.Text = Path.GetFileName(currentGpxPath)

            Case PlaybackState.Playing
                ' Stop mid-route — marker stays where it is, per spec.
                playbackTimer.Stop()
                playbackStatus = PlaybackState.Idle
                btnPlayStop.Text = "Play"
                lblFileName.Text = Path.GetFileName(currentGpxPath)
        End Select
    End Sub

    Private Sub countdownTimer_Tick(sender As Object, e As EventArgs) Handles countdownTimer.Tick
        countdownSecondsRemaining -= 1
        If countdownSecondsRemaining <= 0 Then
            countdownTimer.Stop()
            StartPlayback()
        Else
            lblFileName.Text = $"Starting in {countdownSecondsRemaining}s..."
        End If
    End Sub

    Private Sub StartPlayback()
        playbackStatus = PlaybackState.Playing
        routeStartTime = currentRoute(0).Time
        routeEndTime = currentRoute(currentRoute.Count - 1).Time
        playbackStartTime = DateTime.UtcNow
        currentSegmentIndex = 0
        trackPlayback.Value = trackPlayback.Minimum
        lblFileName.Text = Path.GetFileName(currentGpxPath)
        playbackTimer.Start()
    End Sub

    Private Async Sub playbackTimer_Tick(sender As Object, e As EventArgs) Handles playbackTimer.Tick
        Dim elapsedWallSeconds As Double = (DateTime.UtcNow - playbackStartTime).TotalSeconds
        Dim targetRouteTime As DateTime = routeStartTime.AddSeconds(elapsedWallSeconds)

        If targetRouteTime >= routeEndTime Then
            ' Reached the end of the route — stop and hold the marker at the final point.
            playbackTimer.Stop()
            playbackStatus = PlaybackState.Idle
            btnPlayStop.Text = "Play"

            Dim lastPoint = currentRoute(currentRoute.Count - 1)
            Await PushMarkerToMap(lastPoint.Lat, lastPoint.Lon)
            trackPlayback.Value = trackPlayback.Maximum
            Return
        End If

        ' Advance to the correct segment (pair of consecutive GPX points) for the current time.
        While currentSegmentIndex < currentRoute.Count - 2 AndAlso currentRoute(currentSegmentIndex + 1).Time <= targetRouteTime
            currentSegmentIndex += 1
        End While

        Dim p0 = currentRoute(currentSegmentIndex)
        Dim p1 = currentRoute(currentSegmentIndex + 1)
        Dim segmentDuration As Double = (p1.Time - p0.Time).TotalSeconds

        Dim t As Double = 0
        If segmentDuration > 0 Then
            t = (targetRouteTime - p0.Time).TotalSeconds / segmentDuration
            t = Math.Max(0, Math.Min(1, t)) ' clamp for safety
        End If

        ' Linear interpolation within the segment — fine at this point spacing (seconds, meters).
        Dim lat As Double = p0.Lat + (p1.Lat - p0.Lat) * t
        Dim lon As Double = p0.Lon + (p1.Lon - p0.Lon) * t

        Await PushMarkerToMap(lat, lon)

        ' Update the slider to reflect overall progress through the route.
        Dim totalDuration As Double = (routeEndTime - routeStartTime).TotalSeconds
        If totalDuration > 0 Then
            Dim fraction As Double = elapsedWallSeconds / totalDuration
            trackPlayback.Value = CInt(Math.Max(trackPlayback.Minimum, Math.Min(trackPlayback.Maximum, fraction * trackPlayback.Maximum)))
        End If
    End Sub

    Private Async Function PushMarkerToMap(lat As Double, lon As Double) As Task
        Dim script As String = $"setMarker({lat.ToString(Globalization.CultureInfo.InvariantCulture)}, {lon.ToString(Globalization.CultureInfo.InvariantCulture)});"
        Await webView.ExecuteScriptAsync(script)
    End Function

End Class