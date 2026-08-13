using System.Drawing;
using System.IO;
using System.Reflection;
using Vector.PanelControlPlugin;

namespace SRS3.E2E.PanelControl
{
    public sealed class ControlLibraryImpl : IPanelControlPluginLibrary
    {
        public string LibraryName
        {
            get { return "SRS3 E2E Controls"; }
        }

        public System.Drawing.Image LibraryImage
        {
            get
            {
                Assembly assembly = typeof(ControlLibraryImpl).Assembly;
                using (Stream stream = assembly.GetManifestResourceStream("SRS3.E2E.PanelControl.Resources.E2EControl.png"))
                {
                    if (stream == null)
                    {
                        return null;
                    }

                    using (Bitmap source = new Bitmap(stream))
                    {
                        return new Bitmap(source);
                    }
                }
            }
        }
    }
}
