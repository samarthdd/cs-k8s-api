namespace Glasswall.CloudProxy.Common.Web.Models
{
    public class FileTypeDetectionResponse
    {
        public FileTypeDetectionResponse(FileType fileType)
        {
            FileType = fileType;
        }

        public FileType FileType { get; }

        public string FileTypeName => FileType.ToString();
    }
}
