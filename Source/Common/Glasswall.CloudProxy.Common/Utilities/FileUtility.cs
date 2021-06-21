using Glasswall.CloudProxy.Common.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.IO;

namespace Glasswall.CloudProxy.Common.Utilities
{
    public class FileUtility : IFileUtility
    {
        private readonly ILogger<FileUtility> _logger;
        private bool _disposedValue;

        public FileUtility(ILogger<FileUtility> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool TryReadFormFile(IFormFile formFile, out byte[] file)
        {
            file = null;

            try
            {
                using MemoryStream ms = new MemoryStream();
                formFile.CopyTo(ms);
                file = ms.ToArray();
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, ex, "Could not parse input file.");
            }

            return file?.Length > 0;
        }

        public bool TryGetBase64File(string base64File, out byte[] file)
        {
            file = null;

            try
            {
                file = Convert.FromBase64String(base64File);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not parse base64 file {0}", base64File);
            }

            int fileSize = file?.Length ?? 0;
            return fileSize > 0;
        }

        public FileTypeDetectionResponse DetermineFileType(byte[] fileBytes)
        {
            if (fileBytes == null)
            {
                throw new ArgumentNullException(nameof(fileBytes));
            }

            FileType fileType = FileType.Unknown;

            try
            {
                string magicNumber = GetMagicNumber(fileBytes);

                switch (magicNumber)
                {
                    case string s when s.StartsWith(Constants.MagicNumbers.DOCX_PPTX_XLSX):
                    case string r when r.StartsWith(Constants.MagicNumbers.DOCX_PPTX_XLSX_):
                        {
                            fileType = FileType.Docx_Pptx_Xlsx;
                        }
                        break;
                    case string s when s.StartsWith(Constants.MagicNumbers.RAR_V5):
                    case string r when r.StartsWith(Constants.MagicNumbers.RAR_V4):
                        {
                            fileType = FileType.Rar;
                        }
                        break;
                    case string s when s.StartsWith(Constants.MagicNumbers._7Z):
                        {
                            fileType = FileType.SevenZip;
                        }
                        break;
                    case string s when s.StartsWith(Constants.MagicNumbers.TAR):
                        {
                            fileType = FileType.Tar;
                        }
                        break;
                    case string s when s.StartsWith(Constants.MagicNumbers.ZIP):
                        {
                            fileType = FileType.Zip;
                        }
                        break;
                    case string s when s.StartsWith(Constants.MagicNumbers.GZIP):
                        {
                            fileType = FileType.Gzip;
                        }
                        break;
                    case string s when s.StartsWith(Constants.MagicNumbers.TAR_BZ2):
                        {
                            fileType = FileType.Tar;
                        }
                        break;
                    case string s when s.StartsWith(Constants.MagicNumbers.TAR_LZH):
                    case string r when r.StartsWith(Constants.MagicNumbers.TAR_LZW):
                        {
                            fileType = FileType.Tar;
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Warning, $"Defaulting 'FileType' to {FileType.Unknown} due to {ex.Message} and {ex.StackTrace}");
            }

            return new FileTypeDetectionResponse(fileType);
        }

        private string GetMagicNumber(byte[] fileBytes)
        {
            try
            {
                byte[] buffer;
                using (Stream stream = new MemoryStream(fileBytes))
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    buffer = reader.ReadBytes(16);
                }

                string hex = BitConverter.ToString(buffer);
                return hex.Replace("-", " ").ToUpper();
            }
            catch (Exception ex)
            {
                _logger.LogError($"{ex.Message} and Error detail is {ex.StackTrace}");
            }

            return null;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                }

                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
