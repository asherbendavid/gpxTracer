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

        AddHandler Me.Load, AddressOf MainForm_Load
    End Sub

    Private Async Sub MainForm_Load(sender As Object, e As EventArgs)
        ' WebView2 must be initialized before any navigation or script execution.
        Await webView.EnsureCoreWebView2Async()
        ' TODO: Navigate to the local Leaflet HTML file once it's added to the project.
    End Sub

    Private Sub btnLoadGpx_Click(sender As Object, e As EventArgs) Handles btnLoadGpx.Click
        Using ofd As New OpenFileDialog()
            ofd.Filter = "GPX files (*.gpx)|*.gpx"
            ofd.Title = "Load GPX Route"

            If Not String.IsNullOrEmpty(My.Settings.LastGpxFolder) AndAlso Directory.Exists(My.Settings.LastGpxFolder) Then
                ofd.InitialDirectory = My.Settings.LastGpxFolder
            End If

            If ofd.ShowDialog() = DialogResult.OK Then
                ' TODO: Parse GPX here. If parsing fails due to missing timestamps,
                ' show a MessageBox, and leave currentGpxPath / loaded route untouched.

                currentGpxPath = ofd.FileName
                lblFileName.Text = Path.GetFileName(currentGpxPath)
                btnPlayStop.Enabled = True

                My.Settings.LastGpxFolder = Path.GetDirectoryName(currentGpxPath)
                My.Settings.Save()
            End If
        End Using
    End Sub

    Private Sub btnPlayStop_Click(sender As Object, e As EventArgs) Handles btnPlayStop.Click
        isPlaying = Not isPlaying
        btnPlayStop.Text = If(isPlaying, "Stop", "Play")
        playbackTimer.Enabled = isPlaying
        ' TODO: On stop, decide whether to hold position or reset to start.
    End Sub

    Private Sub playbackTimer_Tick(sender As Object, e As EventArgs) Handles playbackTimer.Tick
        ' TODO: Advance interpolated position along the route, update trackPlayback.Value,
        ' and push the new lat/lon to Leaflet via webView.ExecuteScriptAsync().
    End Sub

End Class