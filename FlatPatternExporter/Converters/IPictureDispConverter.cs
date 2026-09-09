using stdole;

using System.Drawing;

namespace FlatPatternExporter.Converters;

public class IPictureDispConverter : System.Windows.Forms.AxHost
{
    private IPictureDispConverter() : base("")
    {
    }

    public static Image? PictureDispToImage(IPictureDisp pictureDisp)
    {
        try
        {
            return GetPictureFromIPicture(pictureDisp);
        }
        catch
        {
            return null;
        }
    }
}
