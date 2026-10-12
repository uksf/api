using UKSF.Api.ArmaServer.Models.Persistence;
using static UKSF.Api.ArmaServer.Converters.PersistenceConversionHelpers;

namespace UKSF.Api.ArmaServer.Converters;

public static class PersistenceMarkerConverter
{
    private const string PolylineShape = "POLYLINE";
    private const int PolylineFieldCount = 6;
    private const int StandardFieldCount = 10;

    public static PersistenceMarker FromList(List<object> raw)
    {
        var name = raw.Count > 0 ? ToSafeString(raw[0]) : string.Empty;
        var shape = raw.Count > 2 ? ToSafeString(raw[2]) : string.Empty;
        var isPolyline = shape == PolylineShape;
        if (raw.Count != (isPolyline ? PolylineFieldCount : StandardFieldCount))
        {
            throw new FormatException($"Marker '{name}' has {raw.Count} fields, which does not match shape {shape}");
        }

        var marker = new PersistenceMarker { Name = name, Position = Numbers(raw[1]), Shape = shape };
        if (isPolyline)
        {
            marker.Colour = ToSafeString(raw[3]);
            marker.Alpha = ToDouble(raw[4]);
            marker.Polyline = Numbers(raw[5]);
            return marker;
        }

        marker.Type = ToSafeString(raw[3]);
        marker.Brush = ToSafeString(raw[4]);
        marker.Size = Numbers(raw[5]);
        marker.Colour = ToSafeString(raw[6]);
        marker.Alpha = ToDouble(raw[7]);
        marker.Direction = ToDouble(raw[8]);
        marker.Text = ToSafeString(raw[9]);
        return marker;
    }

    public static List<object> ToList(PersistenceMarker marker) =>
        marker.Shape == PolylineShape
            ? [marker.Name, Objects(marker.Position), marker.Shape, marker.Colour, marker.Alpha, Objects(marker.Polyline)]
            :
            [
                marker.Name, Objects(marker.Position), marker.Shape, marker.Type, marker.Brush, Objects(marker.Size), marker.Colour, marker.Alpha,
                marker.Direction ?? 0, marker.Text
            ];

    private static double[] Numbers(object value) => PersistenceConversionHelpers.ToList(value).Select(ToDouble).ToArray();

    private static List<object> Objects(double[] values) => (values ?? []).Select(x => (object)x).ToList();
}
