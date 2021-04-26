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
            public const string SDK_API_VERSION_VALUE = "0.1.8";
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
            public const string REBUILD_ZIP = "rebuildzip";
            public const string REBUILD_ZIP_FROM_BASE64 = "rebuild-zip-from-base64";
            public const string REBUILD_ZIP_FROM_FILE = "rebuild-zip-from-file";
            public const string VERSION = "version";
        }
    }
}
