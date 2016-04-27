using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Script.Serialization;

namespace MediaMethodsWcf
{
    public class GeoLocationService : IGeoLocationService
    {
        private static string ConnectionString = ConfigurationManager.AppSettings.Get("ConnectionString");
        public static JavaScriptSerializer sJavaSerializer = new JavaScriptSerializer();

        private struct Response
        {
            public bool success;
            public string message;
            public object payload;
        }

        private struct LocationObject
        {
            public int id;
            public string name;
            public double latitude;
            public double longitude;
            public int radius;

            public string startTime;
            public string stopTime;

            public string image_icon_url;
            public string image_big_url;

            public string title_en;
            public string msg_en;

            public string title_ms;
            public string msg_ms;

            public string title_zh;
            public string msg_zh;

            public bool enabled;
        }

        private struct LocationRequest
        {
            public double distanceToClosest;
        }

        private struct AdminsObject
        {
            public int id;
            public string name;
            public string username;
            public string token;
        }

        private struct StatisticObject
        {
            public int id;
            public string datetime;
            public int locationId;
            public string identifier;
        }

        private bool checkDuplicateNotification(string userId, int locationId)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(ConnectionString))
                {
                    if (conn.State != System.Data.ConnectionState.Open)
                        conn.Open();

                    MySqlCommand command = conn.CreateCommand();
                    command.CommandText = String.Format("Select top 1 * from statistics where user_identifier='{0}' and location_id={1} order by id desc", userId, locationId);

                    MySqlDataReader reader = command.ExecuteReader();
                    if (reader.Read())
                    {
                        // get time difference from last push
                        TimeSpan ts = DateTime.UtcNow - DateTime.Parse(reader["datetime"].ToString());
                        if (ts.TotalHours < 1)
                        {
                            return true;
                        }
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        private void addStatistic(int locationId, string userIdentifier)
        {
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                if (conn.State != System.Data.ConnectionState.Open)
                    conn.Open();

                MySqlCommand command = conn.CreateCommand();
                command.CommandText = String.Format("Insert into statistics (datetime, location_id, user_identifier) values ('{0}', {1}, '{2}')",
                    DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"), locationId, userIdentifier);
                command.ExecuteNonQuery();
            }
        }

        public string UpdateLocation(string id, double latitude, double longitude)
        {
            // get all the locations which is enabled
            var locationList = getLocationList().FindAll(
                delegate(LocationObject locationObj) {
                    return locationObj.enabled;
                });

            // measure the distance between geolocation and current location
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
                    // check if the user have receive same notification 1 hour ago
                    if(checkDuplicateNotification(id, locObj.id))
                    {
                        // push notification
                        pushNotification(id, locObj.image_big_url, locObj.image_icon_url,
                            locObj.title_en, locObj.msg_en, locObj.title_ms, locObj.msg_ms, locObj.title_zh, locObj.msg_zh);

                        // add to statistic table
                        addStatistic(locObj.id, id);

                        return sJavaSerializer.Serialize(new Response()
                        {
                            success = false,
                            payload = new LocationRequest()
                            {
                                distanceToClosest = closeDistance
                            }
                        });
                    }
                }
            }

            return sJavaSerializer.Serialize(new Response()
            {
                success = false,
                payload = new LocationRequest()
                {
                    distanceToClosest = closeDistance
                }
            });
        }

        public string FirstTimeLogin(string username)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(ConnectionString))
                {
                    if (conn.State != System.Data.ConnectionState.Open)
                        conn.Open();

                    MySqlCommand command = conn.CreateCommand();
                    command.CommandText = String.Format("Select * from admins where username='{0}'", username);

                    MySqlDataReader reader = command.ExecuteReader();
                    if (false == reader.Read())
                    {
                        return sJavaSerializer.Serialize(new Response() { success = false });
                    }

                    if((string)reader["password"] != string.Empty)
                    {
                        return sJavaSerializer.Serialize(new Response() { success = false });
                    }

                    // generate new token
                    string newToken = Guid.NewGuid().ToString();

                    AdminsObject adminObj = new AdminsObject()
                    {
                        id = (int)reader["id"],
                        name = (string)reader["display_name"],
                        username = username,
                        token = newToken
                    };

                    // update the token with new validity
                    reader.Close();
                    command.CommandText = String.Format("Update admins set token='{1}', validity='{2}' where username='{0}'", 
                        username, newToken, DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
                    command.ExecuteNonQuery();


                    return sJavaSerializer.Serialize(new Response() { success = true, payload = adminObj });
                }
            }
            catch (Exception ex)
            {
                return sJavaSerializer.Serialize(new Response() { success = false, message = ex.Message });
            }
        }

        public string Login(string username, string password)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(ConnectionString))
                {
                    if (conn.State != System.Data.ConnectionState.Open)
                        conn.Open();

                    MySqlCommand command = conn.CreateCommand();
                    command.CommandText = String.Format("Select * from admins where username='{0}'", username);

                    MySqlDataReader reader = command.ExecuteReader();
                    if (false == reader.Read())
                    {
                        return sJavaSerializer.Serialize(new Response() { success = false });
                    }

                    // extend the validity by 1 hour
                    refreshToken((int)reader["id"]);

                    // get the new data
                    reader.Close();
                    reader = command.ExecuteReader();
                    if (reader.Read())
                    {
                        AdminsObject adminObj = new AdminsObject()
                        {
                            id = (int)reader["id"],
                            name = (string)reader["display_name"],
                            username = (string)reader["username"],
                            token = (string)reader["token"]
                        };

                        return sJavaSerializer.Serialize(new Response() { success = true, payload = adminObj });
                    }
                } 
            }
            catch (Exception ex)
            {
                return sJavaSerializer.Serialize(new Response() { success = false, message = ex.Message });
            }

            return sJavaSerializer.Serialize(new Response() { success = false });
        }

        public string EditPassword(string oldPassword, string newPassword)
        {
            try
            {
                if (false == checkAuthentication())
                {
                    return sJavaSerializer.Serialize(new Response() { success = false, message = "Invalid login" });
                }

                IncomingWebRequestContext request = WebOperationContext.Current.IncomingRequest;
                WebHeaderCollection headers = request.Headers;

                string[] authentication = headers["Authorization"].Split(':');
                string adminId = authentication[0];
                string token = authentication[1];

                using (MySqlConnection conn = new MySqlConnection(ConnectionString))
                {
                    if (conn.State != System.Data.ConnectionState.Open)
                        conn.Open();

                    // compare old password
                    MySqlCommand command = conn.CreateCommand();
                    command.CommandText = String.Format("Select * from admins where id='{0}'", adminId);
                    MySqlDataReader reader = command.ExecuteReader();
                    if (reader.Read())
                    {
                        if(string.Compare((string)reader["password"], oldPassword) != 0)
                        {
                            return sJavaSerializer.Serialize(new Response() { success = false });
                        }
                    }

                    reader.Close();
                    command.CommandText = String.Format("Update admins set password='{1}' where id='{0}'", adminId, newPassword);
                    command.ExecuteNonQuery();
                }

                return sJavaSerializer.Serialize(new Response() { success = true });
            }
            catch (Exception ex)
            {
                return sJavaSerializer.Serialize(new Response() { success = false, message = ex.Message });
            }
        }

        public string AddLocation(string name, float longitude, float latitude, string image, 
            int radius, string startTime, string endTime, string imageIcon, string imageBig, 
            string enTitle, string enMsg, string msTitle, string msMsg, string zhTitle, string zhMsg)
        {
            try
            {
                if (false == checkAuthentication())
                {
                    return sJavaSerializer.Serialize(new Response() { success = false, message = "Invalid login" });
                }

                using (MySqlConnection conn = new MySqlConnection(ConnectionString))
                {
                    if (conn.State != System.Data.ConnectionState.Open)
                        conn.Open();

                    MySqlCommand command = conn.CreateCommand();
                    command.CommandText = String.Format("Insert into admins (name, longitude, latitude, radius, start_time, end_time, en_title, en_message, image_icon, image_big, ms_title, ms_message, zh_title, zh_message)" +
                        " values ('{0}', {1}, {2}, {3}, '{4}', '{5}', '{6}', '{7}', '{8}', '{9}', '{10}', '{11}', '{12}', '{13}')",
                        name, longitude, latitude, radius,
                        startTime, endTime, enTitle, enMsg, imageIcon, imageBig,
                        msTitle, msMsg, zhTitle, zhMsg);

                    command.ExecuteNonQuery();
                }

                return sJavaSerializer.Serialize(new Response() { success = true });
            }
            catch (Exception ex)
            {
                return sJavaSerializer.Serialize(new Response() { success = false, message = ex.Message });
            }
        }

        public string EditLocation(int locationId, string name, float longitude, float latitude, 
            int radius, string startTime, string endTime, string imageIcon, 
            string imageBig, string enTitle, string enMsg, string msTitle, string msMsg, string zhTitle, string zhMsg)
        {
            try
            {
                if (false == checkAuthentication())
                {
                    return sJavaSerializer.Serialize(new Response() { success = false, message = "Invalid login" });
                }

                using (MySqlConnection conn = new MySqlConnection(ConnectionString))
                {
                    if (conn.State != System.Data.ConnectionState.Open)
                        conn.Open();

                    MySqlCommand command = conn.CreateCommand();
                    command.CommandText = String.Format("Update locations set name='{1}', longitude={2}, latitude={3}, radius={4}, start_time='{5}', end_time='{6}'," + 
                        "en_title='{7}', en_message='{8}', image_icon='{9}', image_big='{10}', ms_title='{11}', ms_message='{12}', zh_title='{13}', zh_message='{14}' where id={0}", 
                        locationId,
                        name, longitude, latitude, radius,
                        startTime, endTime, enTitle, enMsg, imageIcon, imageBig,
                        msTitle, msMsg, zhTitle, zhMsg);

                    command.ExecuteNonQuery();
                }

                return sJavaSerializer.Serialize(new Response() { success = true });
            }
            catch (Exception ex)
            {
                return sJavaSerializer.Serialize(new Response() { success = false, message = ex.Message });
            }
        }

        public string EnabledLocation(int locationId, bool enable)
        {
            try
            {
                if (false == checkAuthentication())
                {
                    return sJavaSerializer.Serialize(new Response() { success = false, message = "Invalid login" });
                }

                using (MySqlConnection conn = new MySqlConnection(ConnectionString))
                {
                    if (conn.State != System.Data.ConnectionState.Open)
                        conn.Open();

                    MySqlCommand command = conn.CreateCommand();
                    command.CommandText = String.Format("Update locations set enabled={0} where id={1}", enable? 1:0, locationId);
                    command.ExecuteNonQuery();
                }

                return sJavaSerializer.Serialize(new Response() { success = true });
            }
            catch (Exception ex)
            {
                return sJavaSerializer.Serialize(new Response() { success = false, message = ex.Message });
            }
        }

        public string AddAdmin(string username, string displayName)
        {
            try
            {
                if (false == checkAuthentication())
                {
                    return sJavaSerializer.Serialize(new Response() { success = false, message = "Invalid login" });
                }

                if(false == isPhoneNumber(username))
                {
                    return sJavaSerializer.Serialize(new Response() { success = false, message = "Expect phone number" });
                }

                using (MySqlConnection conn = new MySqlConnection(ConnectionString))
                {
                    if (conn.State != System.Data.ConnectionState.Open)
                        conn.Open();

                    MySqlCommand command = conn.CreateCommand();
                    command.CommandText = String.Format("Insert into admins (username, display_name) values ('{0}', '{1}')", username, displayName);
                    command.ExecuteNonQuery();

                    // send notification to new user
                    String message = HttpUtility.UrlEncode("Media Methods Sdn. Bhd.%0A%0APlease click following link to access your admin account:%0A" + "http://mediamethods.my/admin?id=" + username);
                    sendSMSToURL("http://www.isms.com.my/isms_send.php?un=" + ConfigurationManager.AppSettings.Get("ismsUsername") 
                        + "&pwd=" + ConfigurationManager.AppSettings.Get("ismsPassword") 
                        + "&dstno=" + username + "&msg=" + message + "&type=1");
                }

                return sJavaSerializer.Serialize(new Response() { success = true });
            }
            catch (Exception ex)
            {
                return sJavaSerializer.Serialize(new Response() { success = false, message = ex.Message });
            }
        }

        private bool isPhoneNumber(string number)
        {
            return Regex.Match(number, @"^[0-9]").Success;
        }

        private string sendSMSToURL(string getUri)
        {
            string SentResult = String.Empty;

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(getUri);

            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            StreamReader responseReader = new StreamReader(response.GetResponseStream());

            String resultmsg = responseReader.ReadToEnd();
            responseReader.Close();

            int StartIndex = 0;
            int LastIndex = resultmsg.Length;

            if (LastIndex > 0)
                SentResult = resultmsg.Substring(StartIndex, LastIndex);

            responseReader.Dispose();

            return SentResult;
        }

        public string EditAdmin(int adminId, string displayName)
        {
            try
            {
                if (false == checkAuthentication())
                {
                    return sJavaSerializer.Serialize(new Response() { success = false, message = "Invalid login" });
                }

                using (MySqlConnection conn = new MySqlConnection(ConnectionString))
                {
                    if (conn.State != System.Data.ConnectionState.Open)
                        conn.Open();

                    MySqlCommand command = conn.CreateCommand();
                    command.CommandText = String.Format("Update admins set display_name='{1}' where id={0}", adminId, displayName);
                    command.ExecuteNonQuery();
                }

                return sJavaSerializer.Serialize(new Response() { success = true });
            }
            catch (Exception ex)
            {
                return sJavaSerializer.Serialize(new Response() { success = false, message = ex.Message });
            }
        }

        public string RemoveAdmin(int adminId)
        {
            try
            {
                if (false == checkAuthentication())
                {
                    return sJavaSerializer.Serialize(new Response() { success = false, message = "Invalid login" });
                }

                using(MySqlConnection conn = new MySqlConnection(ConnectionString))
                {
                    if (conn.State != System.Data.ConnectionState.Open)
                        conn.Open();

                    MySqlCommand command = conn.CreateCommand();
                    command.CommandText = String.Format("Remove from admins where id={0}", adminId);
                    command.ExecuteNonQuery();
                }

                return sJavaSerializer.Serialize(new Response() { success = true });
            }
            catch (Exception ex)
            {
                return sJavaSerializer.Serialize(new Response() { success = false, message = ex.Message });
            }
        }

        private void refreshToken(int adminId)
        {
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                if (conn.State != System.Data.ConnectionState.Open)
                    conn.Open();

                MySqlCommand command = conn.CreateCommand();
                command.CommandText = String.Format("Update admins set validity='{1}', last_login='{2}', token='{3}' where id={0}",
                    adminId,
                    DateTime.UtcNow.AddHours(1).ToString("yyyy-MM-dd HH:mm:ss"),
                    DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                    Guid.NewGuid().ToString());

                command.ExecuteNonQuery();
            }
        }

        private bool checkAuthentication()
        {
            IncomingWebRequestContext request = WebOperationContext.Current.IncomingRequest;
            WebHeaderCollection headers = request.Headers;

            string[] authentication = headers["Authorization"].Split(':');
            if (authentication.Length != 2)
            {
                return false;
            }

            string adminId = authentication[0];
            string token = authentication[1];

            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                if (conn.State != System.Data.ConnectionState.Open)
                    conn.Open();

                MySqlCommand command = conn.CreateCommand();
                command.CommandText = String.Format("Select * from admins where id={0}", adminId);

                MySqlDataReader reader = command.ExecuteReader();
                if (reader.Read())
                {
                    int result = DateTime.UtcNow.CompareTo(reader["validity"]);
                    if (result > 0)
                    {
                        // token expired
                        return false;
                    }

                    if(string.Compare(token, (string)reader["token"]) != 0)
                    {
                        // token not valid
                        return false;
                    }

                    // extend the validity by 1 hour
                    extendTokenValidity(int.Parse(adminId));

                    return true;
                }
            }

            return false;
        }

        private void extendTokenValidity(int adminId)
        {

            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                if (conn.State != System.Data.ConnectionState.Open)
                    conn.Open();

                MySqlCommand command = conn.CreateCommand();
                command.CommandText = String.Format("Update admins set validity='{1}', last_login='{2}' where id={0}", 
                    adminId, 
                    DateTime.UtcNow.AddHours(1).ToString("yyyy-MM-dd HH:mm:ss"), 
                    DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));

                command.ExecuteNonQuery();
            }
        }

        public string GetStatistics(int locationId)
        {
            try
            {
                if(false == checkAuthentication())
                {
                    return sJavaSerializer.Serialize(new Response() { success = false, message = "Invalid login" });
                }

                List<StatisticObject> objectList = new List<StatisticObject>();
                using (MySqlConnection conn = new MySqlConnection(ConnectionString))
                {
                    if (conn.State != System.Data.ConnectionState.Open)
                        conn.Open();

                    MySqlCommand command = conn.CreateCommand();
                    command.CommandText = String.Format("Select * from statistics where location_id={0}", locationId);

                    MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        objectList.Add(new StatisticObject()
                        {
                            id = (int)reader["id"],
                            locationId = (int)reader["location_id"],
                            datetime = reader["datetime"].ToString(),
                            identifier = (string)reader["user_identifier"],
                        });
                    }
                }

                return sJavaSerializer.Serialize(new Response() { success = true, payload = objectList });
            }
            catch (Exception ex)
            {
                return sJavaSerializer.Serialize(new Response() { success = false, message = ex.Message });
            }
        }

        public string GetAllLocations()
        {
            // query from database
            try
            {
                return sJavaSerializer.Serialize(new Response() { success = true, payload = getLocationList() });
            }
            catch (Exception ex)
            {
                return sJavaSerializer.Serialize(new Response() { success = false, message = ex.Message });
            }
        }

        private List<LocationObject> getLocationList()
        {
            List<LocationObject> objectList = new List<LocationObject>();
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                if (conn.State != System.Data.ConnectionState.Open)
                    conn.Open();

                MySqlCommand command = conn.CreateCommand();
                command.CommandText = String.Format("Select * from locations");

                MySqlDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    objectList.Add(new LocationObject()
                    {
                        id = (int)reader["id"],
                        longitude = (float)reader["longitude"],
                        latitude = (float)reader["latitude"],
                        radius = (int)reader["radius"],
                        startTime = reader["start_time"].ToString(),
                        stopTime = reader["end_time"].ToString(),
                        name = (string)reader["name"],
                        enabled = (int)reader["enabled"] == 0 ? false : true,
                        image_big_url = (string)reader["image_big"],
                        image_icon_url = (string)reader["image_icon"],
                        msg_en = (string)reader["en_message"],
                        title_en = (string)reader["en_title"],
                        msg_ms = (string)reader["ms_message"],
                        title_ms = (string)reader["ms_title"],
                        msg_zh = (string)reader["zh_message"],
                        title_zh = (string)reader["zh_title"]
                    });
                }
            }

            return objectList;
        }

        private void pushNotification(string identifier, string androidImageBig, string androidImageIcon,
            string en_title, string en_message,
            string ms_title, string ms_message,
            string zh_title, string zh_message)
        {
            // send push notification if location was within boundary 
            var request = WebRequest.Create("https://onesignal.com/api/v1/notifications") as HttpWebRequest;

            request.KeepAlive = true;
            request.Method = "POST";
            request.ContentType = "application/json";

            request.Headers.Add("authorization", "Basic " + System.Configuration.ConfigurationManager.AppSettings["OneSignalAppAPI"]);

            // TODO: support multiple languages
            byte[] byteArray = Encoding.UTF8.GetBytes("{"
                                                    + "\"app_id\": \"" + System.Configuration.ConfigurationManager.AppSettings["OneSignalAppId"] + "\","
                                                    + "\"contents\": {\"en\": \"" + en_message + "\"},"
                                                    + "\"headings\": {\"en\": \""+ en_message + "\"},"
                                                    + "\"include_player_ids\": [\"" + identifier + "\"]}");

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
            }
        }
    }
}
