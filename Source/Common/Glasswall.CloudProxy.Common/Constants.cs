namespace Glasswall.CloudProxy.Common
{
    public static class Constants
    {
        public const string REPORT_XML_FILE_NAME = "report.xml";
        public const string ERROR_REPORT_HTML_FILE_NAME = "ErrorReport.html";
        public const string METADATA_JSON_FILE_NAME = "metadata.json";
        public const string REPORT_FOLDER_NAME = "report";
        public const string CLEAN_FOLDER_NAME = "clean";
        public const string TRANSACTION_STORE_PATH = "/mnt/stores/transactions";
        public const string VAR_PATH = "/var";
        public const string ZIP_EXTENSION = ".zip";
        public const string STAR = "*";
        public const string CORS_POLICY = "GWApiPolicy";
        public const string OCTET_STREAM_CONTENT_TYPE = "application/octet-stream";
        public const string SWAGGER_URL = "/swagger";
        public const string STATIC_FILES_FOLDER_Name = "StaticFiles";

        public static class Header
        {
            public const string FILE_ID = "X-Adaptation-File-Id";
            public const string VIA = "Via";
            public const string ACCESS_CONTROL_EXPOSE_HEADERS = "Access-Control-Expose-Headers";
            public const string ACCESS_CONTROL_ALLOW_HEADERS = "Access-Control-Allow-Headers";
            public const string ACCESS_CONTROL_ALLOW_ORIGIN = "Access-Control-Allow-Origin";
            public const string SDK_ENGINE_VERSION = "X-SDK-Engine-Version";
            public const string SDK_ENGINE_VERSION_VALUE = "1.157";
            public const string SDK_API_VERSION = "X-SDK-Api-Version";
            public const string SDK_API_VERSION_VALUE = "0.2.4";

            public const string ICAP_FILE_ID = "file-id";
            public const string ICAP_FILE_OUTCOME = "file-outcome";
            public const string ICAP_SOURCE_FILE_LOCATION = "source-file-location";
            public const string ICAP_REBUILT_FILE_LOCATION = "rebuilt-file-location";
            public const string ICAP_GENERATE_REPORT = "generate-report";
            public const string ICAP_SOURCE_PRESIGNED_URL = "source-presigned-url";
            public const string ICAP_REPORT_PRESIGNED_URL = "report-presigned-url";
            public const string ICAP_CLEAN_PRESIGNED_URL = "clean-presigned-url";
            public const string ICAP_REQUEST_MODE = "request-mode";
            public const string ICAP_REQUEST_MODE_VALUE = "respmod";
            public const string ICAP_SDK_ENGINE_VERSION = "rebuild-sdk-version";
            public const string ICAP_REBUILD_PROCESSING_STATUS = "rebuild-processing-status";
            public const string ICAP_GWLOG_PRESIGNED_URL = "gwlog-presigned-url";
            public const string ICAP_LOG_PRESIGNED_URL = "log-presigned-url";
            public const string ICAP_METADATA_PRESIGNED_URL = "metadata-presigned-url";
        }

        public static class EnvironmentVariables
        {
            public const string AWS_ACCESS_KEY_ID = "AwsAccessKeyId";
            public const string AWS_SECRET_ACCESS_KEY = "AwsSecretAccessKey";
        }

        public static class UserAgent
        {
            public const string USER_AGENT = "User-Agent";
            public const string WEB_APP = "Web Application";
            public const string DESKTOP_APP = "Desktop Application";
            public const string OTHERS = "Unknown Application";
            public const string ELECTRON_FAMILY = "electron";
            public const string OTHER_FAMILY = "other";
        }

        public static class Endpoints
        {
            public const string FILE = "file";
            public const string ZIP_FILE = "zipfile";
            public const string BASE64 = "base64";
            public const string PROTECTED_ZIP_FILE = "protectedzipfile";
            public const string XML_REPORT = "xmlreport";
            public const string REBUILD_ZIP_FROM_BASE64 = "rebuild-zip-from-base64";
            public const string REBUILD_ZIP_FROM_FILE = "rebuild-zip-from-file";
            public const string VERSION = "version";
            public const string Default = "/";
        }

        public static class MagicNumbers
        {
            public const string DOCX_PPTX_XLSX = "50 4B 03 04 14 00 06 00";
            public const string DOCX_PPTX_XLSX_ = "50 4B 03 04 14 00 00 00";
            public const string ZIP = "50 4B 03 04";

            public const string RAR_V4 = "52 61 72 21 1A 07 00";
            public const string RAR_V5 = "52 61 72 21 1A 07 01 00";

            public const string _7Z = "37 7A BC AF 27 1C";

            public const string TAR = "75 73 74 61 72";
            public const string TAR_BZ2 = "42 5A 68";   //BZ2, TAR.BZ2, TBZ2, TB2 bzip2 compressed archive DMG
            public const string TAR_LZW = "1F 9D";
            public const string TAR_LZH = "1F A0";

            public const string GZIP = "1F 8B 08";  //GZ, TGZ	 	GZIP archive file VLT VLC Player Skin file
        }
    }
}
