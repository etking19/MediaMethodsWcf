using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Text;
using System.Web.Script.Serialization;

namespace MediaMethodsWcf
{
    // NOTE: You can use the "Rename" command on the "Refactor" menu to change the class name "Service1" in code, svc and config file together.
    // NOTE: In order to launch WCF Test Client for testing this service, please select Service1.svc or Service1.svc.cs at the Solution Explorer and start debugging.
    public class GeoLocationService : IGeoLocationService
    {
        public string GetData(int value)
        {
            return string.Format("You entered: {0}", value);
        }

        public CompositeType GetDataUsingDataContract(CompositeType composite)
        {
            if (composite == null)
            {
                throw new ArgumentNullException("composite");
            }
            if (composite.BoolValue)
            {
                composite.StringValue += "Suffix";
            }
            return composite;
        }

        public static JavaScriptSerializer sJavaSerializer = new JavaScriptSerializer();


        private struct LocationObject
        {
            public int id;
            public double latitude;
            public double longitude;
            public int radius;

            public string name;
            public string startTimeUtc;
            public string stopTimeUtc;

            public string startDateUtc;
            public string stopDateUtc;
        }

        private struct Reponse
        {
            public bool success;
            public string message;
            public object payload;
        }

        private struct ResponseUpdateLocation
        {
            public double distanceToClosest;
        }

        public string GetAllTargets()
        {
            // TODO: query from DB for all the geolocation

            List<LocationObject> locationList = new List<LocationObject>();
            locationList.Add(new LocationObject()
            {
                latitude = 3.130435,
                longitude = 101.625155,
                radius = 200,
                name = "WCT Land Bhd"
            });

            locationList.Add(new LocationObject()
            {
                latitude = 3.17266,
                longitude = 101.67389,
                radius = 200,
                name = "Symphony Life"
            });

            locationList.Add(new LocationObject()
            {
                latitude = 3.060454,
                longitude = 101.470801,
                radius = 200,
                name = "Bukit Raja"
            });

            locationList.Add(new LocationObject()
            {
                latitude = 3.083275,
                longitude = 101.606981,
                radius = 200,
                name = "Yokohama"
            });

            locationList.Add(new LocationObject()
            {
                latitude = 3.083275,
                longitude = 101.606981,
                radius = 200,
                name = "Federal Highway"
            });

            return sJavaSerializer.Serialize(new Reponse()
            {
                success = true,
                payload = locationList
            });
        }

        public string UpdateLocation(string id, double latitude, double longitude)
        {
            // TODO: query from DB for all the geolocation
            // https://www.scribd.com/doc/2569355/Geo-Distance-Search-with-MySQL
            // http://spinczyk.net/blog/2009/10/04/radius-search-with-google-maps-and-mysql/

            List<LocationObject> locationList = new List<LocationObject>();
            locationList.Add(new LocationObject()
            {
                latitude = 3.130435,
                longitude = 101.625155,
                radius = 200,
                name = "WCT Land Bhd"
            });

            locationList.Add(new LocationObject()
            {
                latitude = 3.17266,
                longitude = 101.67389,
                radius = 200,
                name = "Symphony Life"
            });

            locationList.Add(new LocationObject()
            {
                latitude = 3.060454,
                longitude = 101.470801,
                radius = 200,
                name = "Bukit Raja"
            });

            locationList.Add(new LocationObject()
            {
                latitude = 3.083275,
                longitude = 101.606981,
                radius = 200,
                name = "Yokohama"
            });

            locationList.Add(new LocationObject()
            {
                latitude = 3.083275,
                longitude = 101.606981,
                radius = 200,
                name = "Federal Highway"
            });

            // mesaure the distance between geolocation and current location
            double closeDistance = 10000000;
            foreach(LocationObject locObj in locationList)
            {
                double distance = Utils.distance(latitude, longitude, locObj.latitude, locObj.longitude, 'k') * 1000;       // in meter
                if(distance < closeDistance )
                {
                    closeDistance = distance;
                }

                if (distance < (double)locObj.radius)
                {
                    // send push notification if location was within boundary 
                    var request = WebRequest.Create("https://onesignal.com/api/v1/notifications") as HttpWebRequest;

                    request.KeepAlive = true;
                    request.Method = "POST";
                    request.ContentType = "application/json";

                    request.Headers.Add("authorization", "Basic YTJlY2RlYzgtOGI0Ny00MmY2LTlmMDAtNGMzMWNhNzE3NzQy");

                    byte[] byteArray = Encoding.UTF8.GetBytes("{"
                                                            + "\"app_id\": \"1c8cbdb3-6dee-4448-86b1-e3fb67588ee1\","
                                                            + "\"contents\": {\"en\": \"You entered to zone: " + locObj.name + "\"},"
                                                            + "\"url\": \"http://m.wct.com.my/project_details.aspx?pro=99&p=1&t=2\","
                                                            + "\"headings\": {\"en\": \"Congratulation!!\"},"
                                                            + "\"include_player_ids\": [\"" + id + "\"]}");

                    string responseContent = null;

                    try
                    {
                        using (var writer = request.GetRequestStream())
                        {
                            writer.Write(byteArray, 0, byteArray.Length);
                        }

                        using (var response = request.GetResponse() as HttpWebResponse)
                        {
                            using (var reader = new StreamReader(response.GetResponseStream()))
                            {
                                responseContent = reader.ReadToEnd();
                            }
                        }
                    }
                    catch (WebException ex)
                    {
                        System.Diagnostics.Debug.WriteLine(ex.Message);
                        System.Diagnostics.Debug.WriteLine(new StreamReader(ex.Response.GetResponseStream()).ReadToEnd());
                        return sJavaSerializer.Serialize(new Reponse()
                        {
                            success = false,
                            message = ex.Message,
                            payload = new ResponseUpdateLocation()
                            {
                                distanceToClosest = closeDistance
                            }
                        });
                    }

                    return sJavaSerializer.Serialize(new Reponse()
                    {
                        success = true,
                        payload = new ResponseUpdateLocation()
                        {
                            distanceToClosest = closeDistance
                        }
                    });
                }
            }

            return sJavaSerializer.Serialize(new Reponse()
            {
                success = false,
                payload = new ResponseUpdateLocation()
                {
                    distanceToClosest = closeDistance
                }
            });
        }
    }
}
