Imports System.Xml.Linq

''' <summary>
''' A single track point from a GPX file: latitude, longitude, and UTC timestamp.
''' Elevation is intentionally not captured — not needed for this tool.
''' </summary>
Public Structure GpxPoint
    Public ReadOnly Lat As Double
    Public ReadOnly Lon As Double
    Public ReadOnly Time As DateTime ' UTC

    Public Sub New(lat As Double, lon As Double, time As DateTime)
        Me.Lat = lat
        Me.Lon = lon
        Me.Time = time
    End Sub
End Structure

Public Module GpxParser

    ' GPX 1.1 default namespace — trkpt elements live under this, unprefixed in the file.
    Private ReadOnly GpxNs As XNamespace = "http://www.topografix.com/GPX/1/1"

    ''' <summary>
    ''' Parses a GPX file and returns its track points, sorted by time.
    ''' Returns Nothing if the file is malformed or any point is missing a timestamp —
    ''' per the "no partial trust" rule: a route with inconsistent timing data isn't safe
    ''' to use for interpolated playback, so the whole file is rejected rather than
    ''' silently skipping bad points.
    ''' </summary>
    ''' <param name="filePath">Full path to the .gpx file.</param>
    ''' <param name="errorMessage">Set to a human-readable reason if parsing fails.</param>
    Public Function TryParseGpx(filePath As String, ByRef errorMessage As String) As List(Of GpxPoint)
        errorMessage = String.Empty

        Dim doc As XDocument
        Try
            doc = XDocument.Load(filePath)
        Catch ex As Exception
            errorMessage = "The file could not be read as valid XML." & Environment.NewLine & ex.Message
            Return Nothing
        End Try

        Dim trkpts = doc.Descendants(GpxNs + "trkpt").ToList()

        If trkpts.Count = 0 Then
            errorMessage = "No track points (<trkpt>) were found in this file."
            Return Nothing
        End If

        Dim points As New List(Of GpxPoint)

        For Each pt In trkpts
            Dim latAttr = pt.Attribute("lat")
            Dim lonAttr = pt.Attribute("lon")
            Dim timeElem = pt.Element(GpxNs + "time")

            If latAttr Is Nothing OrElse lonAttr Is Nothing Then
                errorMessage = "A track point is missing lat/lon data. The file is invalid."
                Return Nothing
            End If

            If timeElem Is Nothing OrElse String.IsNullOrWhiteSpace(timeElem.Value) Then
                errorMessage = "No time data is included in this file. The file is invalid."
                Return Nothing
            End If

            Dim lat As Double
            Dim lon As Double
            Dim time As DateTime

            If Not Double.TryParse(latAttr.Value, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, lat) OrElse
               Not Double.TryParse(lonAttr.Value, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, lon) Then
                errorMessage = "A track point has an unreadable lat/lon value. The file is invalid."
                Return Nothing
            End If

            ' Timestamps are ISO 8601 UTC (e.g. 2026-07-01T15:00:06.883Z).
            If Not DateTime.TryParse(timeElem.Value, Globalization.CultureInfo.InvariantCulture,
                                      Globalization.DateTimeStyles.AdjustToUniversal Or Globalization.DateTimeStyles.AssumeUniversal,
                                      time) Then
                errorMessage = "A track point has an unreadable timestamp. The file is invalid."
                Return Nothing
            End If

            points.Add(New GpxPoint(lat, lon, time))
        Next

        ' Defensive sort — files should already be in order, but don't assume it.
        points = points.OrderBy(Function(p) p.Time).ToList()

        Return points
    End Function

End Module
