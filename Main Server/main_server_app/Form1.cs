using System.IO;
using Firebase;
using Firebase.Database;
using Firebase.Database.Query;
using Newtonsoft.Json.Linq;
using Firebase.Database.Streaming;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.Logging;
using System.Collections;
using Microsoft.VisualBasic.ApplicationServices;
using System.Linq.Expressions;
using System.Globalization;
using System.Net.Http;
using System.Net;
using System.IO;
using static System.Windows.Forms.AxHost;

using FireSharp.Config;
using FireSharp.Interfaces;
using FireSharp.Response;
using MySqlX.XDevAPI;
using Newtonsoft.Json;
using MySqlX.XDevAPI.Common;
using System.Configuration;

namespace MainServer
{

    public partial class Form1 : Form
    {
        Firebase.Database.FirebaseClient FireClient;
     //   string StatusText = "";


        IFirebaseConfig config = new FirebaseConfig
        {
            AuthSecret = "Ssanw9rmCXkVYABLZ9pjCX0CECOgIM3bPBCs6zv6",
            BasePath = "https://careconnect-1c393-default-rtdb.firebaseio.com/"
        };
        IFirebaseClient client;


        private DistanceService distanceService;
        private PatientInfo patientInfo;
        Emergency emegency;



        public Form1()
        {
            InitializeComponent();
            FireClient = new Firebase.Database.FirebaseClient("https://careconnect-1c393-default-rtdb.firebaseio.com/");
            distanceService = new DistanceService();
            patientInfo = new PatientInfo();
            patientInfo.LoadData();

        }



        private void Form1_Load(object sender, EventArgs e)
        {
            var obserable = FireClient.Child("CareConnect/Emergency").AsObservable<object>();
            var Subscription = obserable.Subscribe(async snapshot =>
            {
                if (snapshot.EventType == FirebaseEventType.InsertOrUpdate)
                {
                    string CollectionName = snapshot.Key;  // a random id for each record in emergency collection will be generated by flutter app
                    emegency = new Emergency();
                    emegency.Ambulance = await FireClient.Child($"CareConnect/Emergency/{CollectionName}/AmbulaceId").OnceSingleAsync<string>();
                    emegency.FingerPrint = await FireClient.Child($"CareConnect/Emergency/{CollectionName}/FingerPrint").OnceSingleAsync<string>();
                    emegency.Location = await FireClient.Child($"CareConnect/Emergency/{CollectionName}/location").OnceSingleAsync<string>();

                    EmergencyFunctions(emegency);

                }
            });
        }

        private void EmergencyFunctions(Emergency emergency)
        {

            ConfigurationManager.AppSettings["StatusText"] += "New Request for Ambulance : " + emegency.FingerPrint + "\n";
            UpdateStatusTextBox();
            ConfigurationManager.AppSettings["StatusText"] += "Patient ID : " + emegency.Ambulance + "\n";
            ConfigurationManager.AppSettings["StatusText"] += "Patient Name : " + patientInfo.UserNameInfo(emegency.Ambulance) + "\n";
            ConfigurationManager.AppSettings["StatusText"] += "Blood Type : " + patientInfo.BloodInfo(emegency.Ambulance) + "\n";
            ConfigurationManager.AppSettings["StatusText"] += "***************************************\n\n";
            UpdateStatusTextBox();
            distanceService.LoadDataFromDatabase();
            UpdateStatusTextBox();
            distanceService.CalculateDistance(emegency.Location);
            UpdateStatusTextBox();
            distanceService.CalculateDistanceAPI(emegency.Location);

        }



        private async Task DeleteRecord(string collectionName)
        {
            try
            {
                await FireClient.Child($"CareConnect/Emergency/{collectionName}").DeleteAsync();
                MessageBox.Show($"Record {collectionName} deleted successfully.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting record {collectionName}: {ex.Message}");
            }
        }



        private void UpdateStatusTextBox()
        {
            string text = ConfigurationManager.AppSettings["StatusText"];
            Thread.Sleep(1000);
            if (StatusTextBox.InvokeRequired)
            {
                StatusTextBox.Invoke(new Action(() => StatusTextBox.Text = text));
            }
            else
            {
                StatusTextBox.Text = text;

            }

        }

        private void ClearBtn_Click(object sender, EventArgs e)
        {
            ConfigurationManager.AppSettings["StatusText"] = "";
            UpdateStatusTextBox();
        }




        private void AddNewHospital_Click(object sender, EventArgs e)
        {
            AddHospital addHospitalForm = new AddHospital();
            addHospitalForm.Show();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            UpdateStatusTextBox();
        }
    }
}
