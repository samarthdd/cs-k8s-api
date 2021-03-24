namespace Glasswall.CloudProxy.IntegrationTest
{
    public static class Constants
    {
        public const string SAMPLE_PDF_FILE_PATH = "SampleData/Sample.pdf";
        public const string JSON_MEDIA_TYPE = "application/json";
        public const string OCTET_STREAM_MEDIA_TYPE = "application/octet-stream";
        public const string FILE_ID_HEADER = "X-Adaptation-File-Id";
        public const string FILE = "file";
        public const string FILE_NAME = "Sample.pdf";
        public const string APP_SETTINGS_FILE_NAME = "IntegrationSettings.json";

        public static class Endpoints
        {
            public const string ANALYSE_BASE64 = "/api/Analyse/base64";
            public const string FILE_TYPE_DETECTION_BASE64 = "/api/FileTypeDetection/base64";
            public const string REBUILD_BASE64 = "/api/Rebuild/base64";
            public const string REBUILD_FILE = "/api/Rebuild/file";
        }
    }
}
