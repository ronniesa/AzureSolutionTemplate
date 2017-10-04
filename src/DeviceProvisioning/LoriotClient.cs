﻿using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace DeviceProvisioning
{
    public class LoriotClient
    {
        public static async Task<dynamic> ListDevices(TraceWriter log)
        {
            log.Info("Starting getting devices from Loriot");
            using (var client = new HttpClient())
            {
                string url = SetupApiCall(client) + "/devices";
                var result = await client.GetAsync(url);
                string resultContent = await result.Content.ReadAsStringAsync();

                if (!result.IsSuccessStatusCode)
                {
                    //TODO: at the moment Loriot doesn't send htis status if the sensor just exists
                    if (result.StatusCode == HttpStatusCode.Conflict)
                    {
                        log.Warning(String.Format("Sensor just exists in Loriot"));
                    }
                    else
                    {
                        throw new HttpRequestException(result.ReasonPhrase);
                    }
                }
                return JObject.Parse(resultContent);                                  
            }
        }

        public static async Task<string> RegisterNewDevice(dynamic queueItem, TraceWriter log)
        {
            using (var client = new HttpClient())
            {
                string url = SetupApiCall(client) + "/devices";
                dynamic item = new ExpandoObject();
                item.deveui = queueItem.deviceId;
                StringContent httpConent = new StringContent(JsonConvert.SerializeObject(item), Encoding.UTF8, "application/json");
                var result = await client.PostAsync(url, httpConent);
                string resultContent = await result.Content.ReadAsStringAsync();

                if (!result.IsSuccessStatusCode)
                {
                    
                    //TODO: at the moment Loriot doesn't send htis status if the sensor just exists
                    if (result.StatusCode == HttpStatusCode.Conflict)
                    {
                        log.Warning(String.Format("Sensor just exists in Loriot"));
                    }
                    else
                    {
                        //HACK: check if is duplicate record error
                        if(!resultContent.Contains("E11000 duplicate key error"))
                        {
                            throw new HttpRequestException(result.ReasonPhrase);
                        }
                    }
                }

                return resultContent;
            }
        }

        public static async Task<string> DeleteDevice(dynamic queueItem, TraceWriter log)
        {
            using (var client = new HttpClient())
            {
                string url = SetupApiCall(client) + "/device/" + queueItem.deviceId;

                var result = await client.DeleteAsync(url);
                string resultContent = await result.Content.ReadAsStringAsync();

                if (!result.IsSuccessStatusCode)
                {
                    //sensor doesn't exists in loriot, used also Notfound maybe loriot in the future sends the right statuscode
                    if (result.StatusCode == HttpStatusCode.MethodNotAllowed ||
                        result.StatusCode == HttpStatusCode.NotFound)
                    {
                        log.Warning(String.Format("Sensor not exists in Loriot"));
                    }
                    else
                    {
                        throw new HttpRequestException(result.ReasonPhrase);
                    }
                }

                return resultContent;
            }
        }

        private static string SetupApiCall(HttpClient client)
        {
            string apiKey = System.Environment.GetEnvironmentVariable("LoriotApiKey");
            string appKey = System.Environment.GetEnvironmentVariable("LoriotAppKey");
            string baseUrl = System.Environment.GetEnvironmentVariable("LoriotUrl");

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            return baseUrl + appKey;
        }
    }
}
