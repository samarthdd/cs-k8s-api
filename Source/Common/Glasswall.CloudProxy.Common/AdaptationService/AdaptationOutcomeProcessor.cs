using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Glasswall.CloudProxy.Common.AdaptationService
{
    public class AdaptationOutcomeProcessor : IResponseProcessor
    {
        private readonly ILogger<AdaptationOutcomeProcessor> _logger;
        private static readonly Dictionary<AdaptationOutcome, ReturnOutcome> OutcomeMap = new Dictionary<AdaptationOutcome, ReturnOutcome>
        {
            { AdaptationOutcome.Unmodified, ReturnOutcome.GW_UNPROCESSED},
            { AdaptationOutcome.Replace, ReturnOutcome.GW_REBUILT},
            { AdaptationOutcome.Failed, ReturnOutcome.GW_FAILED }
        };

        public AdaptationOutcomeProcessor(ILogger<AdaptationOutcomeProcessor> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public IAdaptationServiceResponse Process(IDictionary<string, object> headers, byte[] body)
        {
            IAdaptationServiceResponse adaptationServiceResponse = new AdaptationServiceResponse();
            try
            {
                Guid fileId = Guid.Empty;
                if (!headers.ContainsKey(Constants.Header.ICAP_FILE_ID))
                {
                    throw NewAdaptationServiceException("Missing File Id");
                }

                string fileIdString = Encoding.UTF8.GetString((byte[])headers[Constants.Header.ICAP_FILE_ID]);
                if (fileIdString == null || !Guid.TryParse(fileIdString, out fileId))
                {
                    _logger.LogError($"Error in FileID: {fileIdString ?? "-"}");
                    adaptationServiceResponse.FileOutcome = ReturnOutcome.GW_ERROR;
                    return adaptationServiceResponse;
                }

                adaptationServiceResponse.FileId = fileId;
                if (!headers.ContainsKey(Constants.Header.ICAP_FILE_OUTCOME))
                {
                    throw NewAdaptationServiceException($"Missing outcome for File Id {fileId}");
                }

                string outcomeString = Encoding.UTF8.GetString((byte[])headers[Constants.Header.ICAP_FILE_OUTCOME]);
                AdaptationOutcome outcome = (AdaptationOutcome)Enum.Parse(typeof(AdaptationOutcome), outcomeString, ignoreCase: true);

                if (!OutcomeMap.ContainsKey(outcome))
                {
                    _logger.LogError($"Returning outcome unmapped: {outcomeString} for File Id {fileId}");
                    adaptationServiceResponse.FileOutcome = ReturnOutcome.GW_ERROR;
                    return adaptationServiceResponse;
                }

                adaptationServiceResponse.FileOutcome = OutcomeMap[outcome];
                FillHeaderValues(adaptationServiceResponse, headers);
                return adaptationServiceResponse;
            }
            catch (ArgumentException aex)
            {
                _logger.LogError($"Unrecognised enumeration processing adaptation outcome {aex.Message}");
                adaptationServiceResponse.FileOutcome = ReturnOutcome.GW_ERROR;
                return adaptationServiceResponse;
            }
            catch (JsonReaderException jre)
            {
                _logger.LogError($"Poorly formated adaptation outcome : {jre.Message}");
                adaptationServiceResponse.FileOutcome = ReturnOutcome.GW_ERROR;
                return adaptationServiceResponse;
            }
            catch (AdaptationServiceClientException asce)
            {
                _logger.LogError($"Poorly formated adaptation outcome : {asce.Message}");
                adaptationServiceResponse.FileOutcome = ReturnOutcome.GW_ERROR;
                return adaptationServiceResponse;
            }
        }

        private AdaptationServiceClientException NewAdaptationServiceException(string message)
        {
            return new AdaptationServiceClientException(message);
        }

        private void FillHeaderValues(IAdaptationServiceResponse adaptationServiceResponse, IDictionary<string, object> headers)
        {
            if (headers.ContainsKey(Constants.Header.ICAP_CLEAN_PRESIGNED_URL))
            {
                adaptationServiceResponse.CleanPresignedUrl = Encoding.UTF8.GetString((byte[])headers[Constants.Header.ICAP_CLEAN_PRESIGNED_URL]);
            }

            if (headers.ContainsKey(Constants.Header.ICAP_REBUILT_FILE_LOCATION))
            {
                adaptationServiceResponse.RebuiltFileLocation = Encoding.UTF8.GetString((byte[])headers[Constants.Header.ICAP_REBUILT_FILE_LOCATION]);
            }

            if (headers.ContainsKey(Constants.Header.ICAP_REPORT_PRESIGNED_URL))
            {
                adaptationServiceResponse.ReportPresignedUrl = Encoding.UTF8.GetString((byte[])headers[Constants.Header.ICAP_REPORT_PRESIGNED_URL]);
            }

            if (headers.ContainsKey(Constants.Header.ICAP_SOURCE_FILE_LOCATION))
            {
                adaptationServiceResponse.SourceFileLocation = Encoding.UTF8.GetString((byte[])headers[Constants.Header.ICAP_SOURCE_FILE_LOCATION]);
            }

            if (headers.ContainsKey(Constants.Header.ICAP_SOURCE_PRESIGNED_URL))
            {
                adaptationServiceResponse.SourcePresignedUrl = Encoding.UTF8.GetString((byte[])headers[Constants.Header.ICAP_SOURCE_PRESIGNED_URL]);
            }
        }
    }
}
