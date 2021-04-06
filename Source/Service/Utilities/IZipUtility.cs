namespace Glasswall.CloudProxy.Api.Utilities
{
    public interface IZipUtility
    {
        /// <summary>
        /// Extract zip to the outFolder
        /// </summary>
        /// <param name="archivePath">zip file path</param>
        /// <param name="password">password</param>
        /// <param name="outFolder">output folder path</param>
        void ExtractZipFile(string archivePath, string password, string outFolder);

        /// <summary>
        /// Create zip file
        /// </summary>
        /// <param name="outPathname">output path of the zip file</param>
        /// <param name="password">password</param>
        /// <param name="folderName">folder path that is going to be zipped</param>
        void CreateZipFile(string outPathname, string password, string folderName);
    }
}
