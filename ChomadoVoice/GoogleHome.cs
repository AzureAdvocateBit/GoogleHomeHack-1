﻿using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using VoiceTextWebAPI.Client;

namespace ChomadoVoice
{
    public static class GoogleHome
    {
        [FunctionName("GoogleHome")]
        public static async Task<HttpResponseMessage> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequestMessage req
            , /* Azure Blob Storage(ファイル置き場) への出力 */ [Blob("mp3/voice.mp3", FileAccess.ReadWrite)] CloudBlockBlob mp3Out
            , TraceWriter log
        )
        {
            try
            {
                log.Info("C# HTTP trigger function processed a request.");
                var data = await req.Content.ReadAsAsync<Models.DialogFlowRequestModel>();
                //log.Info(data);
                var say = data.QueryResult.QueryText;

                // VoiceText Web API に投げる処理 test
                var voiceTextClient = new VoiceTextClient
                {
                    APIKey = Keys.APIKeys.VoiceTextWebApiKey,
                    Speaker = Speaker.Bear,
                    Emotion = Emotion.Anger,
                    EmotionLevel = EmotionLevel.High,
                    Format = Format.MP3
                };
                var bytes = await voiceTextClient.GetVoiceAsync(text: say);

                // Azure Blob Storage への書き込み（保存）
                await mp3Out.UploadFromByteArrayAsync(buffer: bytes, index: 0, count: bytes.Length);

                // Azure Blob Storage に書き込まれた mp3 にアクセスするための URL
                var mp3Url = mp3Out.Uri;
                //DialogFlow v2 では、DialogFlow へのwebhookの応答メッセージ　標準データ形式である必要がある。
                //See https://developers.google.com/assistant/actions/build/json/dialogflow-webhook-json and https://cloud.google.com/dialogflow/docs/reference/rpc/google.cloud.dialogflow.v2#webhookresponse
                var response = new Models.DialogFlowResponseModel
                {
                    Payload = new Models.Payload
                    {
                        Google = new Models.Google
                        {
                            ExpectUserResponse = false,
                            RichResponse = new Models.RichResponse
                            {
                                Items = new Models.Item[]
                                {
                                    new Models.Item
                                    {
                                        SimpleResponse = new Models.SimpleResponse
                                        {
                                            // Google Home に喋らせたい文言を渡す。（この場合mp3）
                                            SSML = $"<speak><audio src='{mp3Url}' /></speak>",
                                            // Google Assistant のチャット画面上に出したい文字列
                                            DisplayText = $"「{say}」"
                                        }
                                    }
                                }
                            }
                        }
                    }
                };
                var result = req.CreateResponse(HttpStatusCode.OK, response);
                result.Headers.Add("ContentType", "application/json");
                return result;
            }
            catch (Exception ex)
            {
                log.Error("An exception occurred in GoogleHome.Run", ex);
                var result = req.CreateErrorResponse(HttpStatusCode.InternalServerError, ex);
                return result;
            }
        }
        
    }
}
