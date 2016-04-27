using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Text;

namespace MediaMethodsWcf
{
    // NOTE: You can use the "Rename" command on the "Refactor" menu to change the interface name "IService1" in both code and config file together.
    [ServiceContract]
    public interface IGeoLocationService
    {
        [OperationContract]
        string Login(string username, string password);

        [OperationContract]
        string FirstTimeLogin(string username);

        [OperationContract]
        string EditPassword(string oldPassword, string newPassword);

        [OperationContract]
        string ForgotPassword(string username);

        [OperationContract]
        string AddLocation(string name, float longitude, float latitude, string image, int radius,
            string startTime, string endTime, string imageIcon, string imageBig,
            string enTitle, string enMsg,
            string msTitle, string msMsg,
            string zhTitle, string zhMsg);

        [OperationContract]
        string EditLocation(int locationId,
            string name, float longitude, float latitude, int radius,
            string startTime, string endTime, string imageIcon, string imageBig,
            string enTitle, string enMsg,
            string msTitle, string msMsg,
            string zhTitle, string zhMsg);

        [OperationContract]
        string EnabledLocation(int locationId, bool enable);

        [OperationContract]
        string AddAdmin(string username, string displayName);

        [OperationContract]
        string EditAdmin(int adminId, string displayName);

        [OperationContract]
        string RemoveAdmin(int adminId);

        [OperationContract]
        string GetStatistics(int locationId);

        [OperationContract]
        string UpdateLocation(string id, double latitude, double longitude);

        [OperationContract]
        string GetAllLocations();
    }
}
