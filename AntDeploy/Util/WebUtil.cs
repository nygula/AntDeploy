﻿using Newtonsoft.Json;
using NLog;
using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace AntDeploy.Util
{
    public class WebUtil
    {
        public static bool SetAllowUnsafeHeaderParsing20()
        {
            try
            {
                ServicePointManager.Expect100Continue = false; 
                ServicePointManager.MaxServicePointIdleTime = 2000; 

                //Get the assembly that contains the internal class
                Assembly aNetAssembly = Assembly.GetAssembly(typeof(System.Net.Configuration.SettingsSection));
                if (aNetAssembly != null)
                {
                    //Use the assembly in order to get the internal type for the internal class
                    Type aSettingsType = aNetAssembly.GetType("System.Net.Configuration.SettingsSectionInternal");
                    if (aSettingsType != null)
                    {
                        //Use the internal static property to get an instance of the internal settings class.
                        //If the static instance isn't created allready the property will create it for us.
                        object anInstance = aSettingsType.InvokeMember("Section",
                            BindingFlags.Static | BindingFlags.GetProperty | BindingFlags.NonPublic, null, null, new object[] { });
                        if (anInstance != null)
                        {
                            //Locate the private bool field that tells the framework is unsafe header parsing should be allowed or not
                            FieldInfo aUseUnsafeHeaderParsing = aSettingsType.GetField("useUnsafeHeaderParsing", BindingFlags.NonPublic | BindingFlags.Instance);
                            if (aUseUnsafeHeaderParsing != null)
                            {
                                aUseUnsafeHeaderParsing.SetValue(anInstance, true);
                                return true;
                            }
                        }
                    }
                }
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool CheckValidationResult(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors errors)
        {
            return true;
        }

        public static bool IsHttpGetOk(string url, Logger logger)
        {
            try
            {
                HttpWebRequest WReq = (HttpWebRequest)WebRequest.Create(url);

                if (url.StartsWith("https"))
                {
                    ServicePointManager.ServerCertificateValidationCallback = new RemoteCertificateValidationCallback(CheckValidationResult);
                }

                WReq.Method = "GET";
                WReq.Timeout = 10000;
                HttpWebResponse WResp = (HttpWebResponse)WReq.GetResponse();
                if (WResp != null)
                {
                    logger.Info($"Response StatusCode:{(int)WResp.StatusCode}");
                    if (((int)WResp.StatusCode)<400)
                    {
                        WResp.Close();
                        return true;
                    }
                    WResp.Close();
                }
            }
            catch (Exception ex1)
            {
                logger.Warn(ex1.Message);
                //ignore
            }
            return false;
        }

        public static async Task<T> HttpPostAsync<T>(string url, object json, Logger logger)
        {
            string result = string.Empty;
            try
            {
                HttpWebRequest WReq = (HttpWebRequest)WebRequest.Create(url);
                WReq.Method = "POST";
                WReq.Timeout = 5000;
                var st = JsonConvert.SerializeObject(json);
                byte[] byteArray = Encoding.UTF8.GetBytes(st);
                WReq.ContentType = "application/json";
                WReq.ContentLength = byteArray.Length;
                using (var newStream = await WReq.GetRequestStreamAsync())
                {
                    await newStream.WriteAsync(byteArray, 0, byteArray.Length);
                }
                HttpWebResponse WResp = (HttpWebResponse)await WReq.GetResponseAsync();
                if (WResp != null)
                {
                    Stream stream = WResp.GetResponseStream();
                    if (stream != null)
                    {
                        var reader = new StreamReader(stream);
                        result = await reader.ReadToEndAsync();
                        reader.Close();
                        stream.Close();
                    }
                    WResp.Close();
                }
                if (!string.IsNullOrEmpty(result))
                {
                    return JsonConvert.DeserializeObject<T>(result);
                }
            }
            catch (Exception ex1)
            {
                logger.Error(ex1.Message);
                //ignore
            }
            return default(T);
        }
    }
}
