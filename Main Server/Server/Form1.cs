﻿using System;
using System.Configuration;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using FireSharp.Config;
using FireSharp.Interfaces;
using FireSharp.Response;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Net;
using System.Threading;
using Firebase.Database.Streaming;
using RestSharp.Contrib;
using System.Drawing.Drawing2D;

namespace Server
{
    public partial class Form1 : Form
    {
        Firebase.Database.FirebaseClient FireClient;
        Dictionary<string, double> DistanceValuesAPI = new Dictionary<string, double>();
        string smsMessage = "";
        int Hospital_number = 342, Hospital_number2 = 0;
        private int borderRadius = 25;
        string Hospital_Selected_Key = ""; 
        string Blood_Request_Hospital = "";

        static IFirebaseConfig config = new FirebaseConfig
        {
            AuthSecret = "Ssanw9rmCXkVYABLZ9pjCX0CECOgIM3bPBCs6zv6",
            BasePath = "https://careconnect-1c393-default-rtdb.firebaseio.com/"
        };
        IFirebaseClient client = new FireSharp.FirebaseClient(config);

        private DistanceService distanceService;
        private PatientInfo patientInfo;
        Emergency emergency;



        public Form1()
        {
            InitializeComponent();
            ProgressBars();
            FireClient = new Firebase.Database.FirebaseClient("https://careconnect-1c393-default-rtdb.firebaseio.com/");
            distanceService = new DistanceService();
            patientInfo = new PatientInfo();
            patientInfo.LoadData();
            RadForm();
          //     MessageBox.Show("ds");


        }


        private void ProgressBars()
        {
            
            Porgress1.Value = 0;

        }



        private void Form1_Load(object sender, EventArgs e)
        {

        //    LoadDataDatabase("1111");


            var obserable = FireClient.Child("CareConnect/Emergency").AsObservable<object>();
            var Subscription = obserable.Subscribe(async snapshot =>
            {
                if (snapshot.EventType == FirebaseEventType.InsertOrUpdate)
                {
                    string CollectionName = snapshot.Key;  // a random id for each record in emergency collection will be generated by flutter app
                    emergency = new Emergency();
                    emergency.Ambulance = await FireClient.Child($"CareConnect/Emergency/{CollectionName}/AmbulaceId").OnceSingleAsync<string>();
                    emergency.FingerPrint = await FireClient.Child($"CareConnect/Emergency/{CollectionName}/FingerPrint").OnceSingleAsync<string>();
                    emergency.Location = await FireClient.Child($"CareConnect/Emergency/{CollectionName}/location").OnceSingleAsync<string>();

                    if (emergency.Ambulance != null)
                    {
                        smsMessage += "تعرض " + GoogleTranslate(patientInfo.UserNameInfo(emergency.FingerPrint)) + " لحادث " +"\n";
                        EmergencyFunctions(emergency);
                        //  DeleteRecord(CollectionName);
                    }

                }
            });
        }






        private void EmergencyFunctions(Emergency emergency)
        {
            GetingStart();
            distanceService.LoadDataFromDatabase();
            UpdateStatusTextBox();
            distanceService.CalculateDistance(this.emergency.Location);
            UpdateStatusTextBox();
            DistanceValuesAPI = distanceService.CalculateDistanceAPI(this.emergency.Location);
            UpdateStatusTextBox();
            CheckFreeBed();
            CheckBloodAvailability(emergency.FingerPrint);
            SendSMS(smsMessage);
            RequestBloodFromHospital();
            RequestToHospitalReception();
            RequestLocationToMobile();

        }

        private void RequestLocationToMobile()
        {
            ConfigurationManager.AppSettings["StatusText"]+= "5) Requesting Location to Mobile App\n\n";
            UpdateStatusTextBox();

        }

        private void RequestToHospitalReception()
        {
            ConfigurationManager.AppSettings["StatusText"]+= "4) The hospital was notified to receive the person : " + GetHospitalName(Hospital_Selected_Key) + "\n\n";
            UpdateStatusTextBox();

        }

        private void RequestBloodFromHospital()
        {
            ConfigurationManager.AppSettings["StatusText"]+= "3) Requesting Blood from : " + GetHospitalName(Hospital_Selected_Key) + "\n\n";
            UpdateStatusTextBox();

        }

        private void SendSMS( string smsMessage)
        {

            ConfigurationManager.AppSettings["StatusText"]+= "2) SMS message was sent successfully!\n\n";
            UpdateStatusTextBox();

        }


        private void CheckBloodAvailability(string fingerprintID)
        {
            Dictionary<string, int> Data = new Dictionary<string, int>();

            Data = LoadBloodTypes(Hospital_Selected_Key);

            ConfigurationManager.AppSettings["StatusText"]+= "Checking Blood Availability for : " + patientInfo.UserNameInfo(fingerprintID) + "\n\n";
            UpdateStatusTextBox();
            ConfigurationManager.AppSettings["StatusText"]+= "Blood Type : " + patientInfo.BloodInfo(fingerprintID) + "\n";
            ConfigurationManager.AppSettings["StatusText"] += " - - - - - - - - - - -\n\n";
            UpdateStatusTextBox();

            if (Check_blood_availability(Hospital_Selected_Key, ref Data, patientInfo.BloodInfo(fingerprintID)))
            {
                ConfigurationManager.AppSettings["StatusText"]+= "1) Blood was found in : " + GetHospitalName(Hospital_Selected_Key) + "\n";
                Blood_Request_Hospital= Hospital_Selected_Key;
                UpdateStatusTextBox();

            }
            else
            {
                ConfigurationManager.AppSettings["StatusText"]+= "Blood was not found in : " + GetHospitalName(Hospital_Selected_Key) + "\n";
                ConfigurationManager.AppSettings["StatusText"]+= "Searching for another hospital ..." + "\n";
                UpdateStatusTextBox();
                SearchBloodInHospitl(ref Data, patientInfo.BloodInfo(fingerprintID));
                
            }
        }

        private void SearchBloodInHospitl(ref Dictionary<string, int> Data, string Blood_Type)
        {

            foreach (var hospital in DistanceValuesAPI)
            {
                if (Check_blood_availability(hospital.Key, ref Data, Blood_Type))
                {
                    ConfigurationManager.AppSettings["StatusText"]+= "Blood was found in : " + GetHospitalName(hospital.Key) + "\n\n";
                    Blood_Request_Hospital = Hospital_Selected_Key;
                    UpdateStatusTextBox();
                    break;
                }
                else
                {
                    ConfigurationManager.AppSettings["StatusText"]+= "Blood was not found in : " + GetHospitalName(hospital.Key) + "\n";
                    ConfigurationManager.AppSettings["StatusText"]+= "Searching for another hospital ..." + "\n\n";
                    UpdateStatusTextBox();
                }
            }
            

        }




        public Dictionary<string, int> LoadBloodTypes(string hospitalKey)
        {
            Dictionary<string, int> HospitalDataDictionaryTemp = new Dictionary<string, int>();
            client = new FireSharp.FirebaseClient(config);
            FirebaseResponse response = client.Get("CareConnect/HospitalData/" + hospitalKey);
            dynamic data = JObject.Parse(response.Body);

            HospitalDataDictionaryTemp.Add("ABPlus", Convert.ToInt32(data["ABPlus"]));
            HospitalDataDictionaryTemp.Add("ABMinus", Convert.ToInt32(data["ABMinus"]));
            HospitalDataDictionaryTemp.Add("APlus", Convert.ToInt32(data["APlus"]));
            HospitalDataDictionaryTemp.Add("AMinus", Convert.ToInt32(data["AMinus"]));
            HospitalDataDictionaryTemp.Add("BPlus", Convert.ToInt32(data["BPlus"]));
            HospitalDataDictionaryTemp.Add("BMinus", Convert.ToInt32(data["BMinus"]));
            HospitalDataDictionaryTemp.Add("OPlus", Convert.ToInt32(data["OPlus"]));
            HospitalDataDictionaryTemp.Add("OMinus", Convert.ToInt32(data["OMinus"]));


           

            string allData= "";
            foreach (var item in HospitalDataDictionaryTemp)
            {
                allData += item.Key + " : " + item.Value + "\n";
            }
            MessageBox.Show(allData);

            return HospitalDataDictionaryTemp;
        }



        private void CheckFreeBed()
        {

            foreach (var hospital in DistanceValuesAPI)
            {
                MessageBox.Show(hospital.Key + "  " + hospital.Value);
                if (Check_Rooms_availability(hospital.Key))
                {

                    ConfigurationManager.AppSettings["StatusText"] += "A free bed was found in : " + GetHospitalName(hospital.Key) + "\n\n";

                    smsMessage += " وتم نقلة الى  " + GoogleTranslate(GetHospitalName(hospital.Key)) + "\n";
                    smsMessage+= "نظرا لتعرضة لحالة طارئة فى تمام الساعة " + DateTime.Now.ToString("h:mm tt") + "\n";
                    smsMessage += "برجاء الذهاب الى المستشفي فى اسرع وقت" + "\n";
                    smsMessage += "عنوان المستشفي : "+"\n" + GetGoogleMapsUrl(GetHospitalAddress(hospital.Key)) + "\n";

                    Hospital_Selected_Key= hospital.Key;
                    MessageBox.Show( smsMessage);
                    UpdateStatusTextBox();
                    break;
                }
                else
                {
                    ConfigurationManager.AppSettings["StatusText"] += "No free bed was found in : " + GetHospitalName(hospital.Key) + "\n\n";
                }
            }

        }

        public string GoogleTranslate(string text)
        {
            string from = "en";
            string to = "ar";
            string url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl={from}&tl={to}&dt=t&q={HttpUtility.UrlEncode(text)}";
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            StreamReader reader = new StreamReader(response.GetResponseStream());
            string result = reader.ReadToEnd();
            reader.Close();
            response.Close();
            JArray jsonData = JArray.Parse(result);
            string translatedText = jsonData[0][0][0].ToString();
            return translatedText;
        }
        public static string GetGoogleMapsUrl(string location)
        {
            string latitude = location.Split(',')[0];
            string longitude = location.Split(',')[1];
            return $"https://www.google.com/maps?q={latitude},{longitude}";
        }
        private string GetHospitalName(string hospitalKey)
        {
            FirebaseResponse DataResponse = client.Get("CareConnect/HospitalData/" + Convert.ToString(hospitalKey));
            JObject HospitalData = JObject.Parse(DataResponse.Body);
            return HospitalData["Name"].ToString();
        }
        private string GetHospitalAddress(string hospitalKey)
        {
            FirebaseResponse DataResponse = client.Get("CareConnect/HospitalData/" + Convert.ToString(hospitalKey));
            JObject HospitalData = JObject.Parse(DataResponse.Body);
            return HospitalData["Address"].ToString();
        }
        private void GetingStart()
        {
            ConfigurationManager.AppSettings["StatusText"] += "New Request for Ambulance : " + this.emergency.FingerPrint + "\n";
            UpdateStatusTextBox();
            ConfigurationManager.AppSettings["StatusText"] += "Patient ID : " + this.emergency.Ambulance + "\n";
            ConfigurationManager.AppSettings["StatusText"] += "Patient Name : " + patientInfo.UserNameInfo(this.emergency.Ambulance) + "\n";
            ConfigurationManager.AppSettings["StatusText"] += "Blood Type : " + patientInfo.BloodInfo(this.emergency.Ambulance) + "\n";
            ConfigurationManager.AppSettings["StatusText"] += "**********************************\n\n";
            UpdateStatusTextBox();
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
            Thread.Sleep(200);
            if (StatusTextBox.InvokeRequired)
            {
                StatusTextBox.Invoke(new Action(() => StatusTextBox.Text = text));
            }
            else
            {
                StatusTextBox.Text = text;

            }
            ScrollControlIntoView(StatusTextBox);

        }
        private void AddNewHospital_Click(object sender, EventArgs e)
        {
          //  AddHospital addHospitalForm = new AddHospital();
        //    addHospitalForm.Show();
        }
        private bool Check_Rooms_availability(string Hospital_Key)
        {
            // ------------- connect to firebase and get the data ----------------
            FirebaseResponse DataResponse = client.Get("CareConnect/HospitalData/" + Convert.ToString(Hospital_Key));
            JObject HospitalData = JObject.Parse(DataResponse.Body);

            // ----------- get info about number of max and current available rooms in this hospital -----------
            int Room_MaxSize_MED = Convert.ToInt32(HospitalData["MaxSize_MED"]);
            int Room_MaxSize_IR = Convert.ToInt32(HospitalData["MaxSize_IR"]);
            int Room_MaxSize_ICU = Convert.ToInt32(HospitalData["MaxSize_ICU"]);
            int Room_MaxSize_EOR = Convert.ToInt32(HospitalData["MaxSize_EOR"]);
            int Room_CurSize_MED = Convert.ToInt32(HospitalData["CurSize_MED"]);
            int Room_CurSize_IR = Convert.ToInt32(HospitalData["CurSize_IR"]);
            int Room_CurSize_ICU = Convert.ToInt32(HospitalData["CurSize_ICU"]);
            int Room_CurSize_EOR = Convert.ToInt32(HospitalData["CurSize_EOR"]);

            if (Room_MaxSize_MED - Room_CurSize_MED > 0)
            {
                Update_Rooms_Values(Hospital_Key, "CurSize_MED", "MED", Room_CurSize_MED - 1);
                return true;
            }
            else if (Room_MaxSize_ICU - Room_CurSize_ICU > 0)
            {
                Update_Rooms_Values(Hospital_Key, "CurSize_ICU", "ICU", Room_CurSize_ICU - 1);
                return true;
            }
            else if (Room_MaxSize_EOR - Room_CurSize_EOR > 0)
            {
                Update_Rooms_Values(Hospital_Key, "CurSize_EOR", "EOR", Room_CurSize_EOR - 1);
                return true;
            }
            else if (Room_MaxSize_IR - Room_CurSize_IR > 0)
            {
                Update_Rooms_Values(Hospital_Key, "CurSize_IR", "IR", Room_CurSize_IR - 1);
                return true;
            }
            else
                return false;
        }
        private async void Update_Rooms_Values(string Hospital_Key, string Room_Name, string Room_Name_In_Date_Category, int New_Value)
        {
            try
            {
                var UpdateData = new Dictionary<string, object>
                {
                    { Room_Name , New_Value }
                };
                await client.UpdateAsync($"CareConnect/HospitalData/{Hospital_Key}/", UpdateData);


                var UpdateDate = new Dictionary<string, object>
                {
                    { Room_Name_In_Date_Category+"_LastEdit" ,  DateTime.Now}
                };
                await client.UpdateAsync($"CareConnect/HospitalData/{Hospital_Key}/", UpdateDate);
            }
            catch (Exception)
            {
                MessageBox.Show("Failed to Update Data, Please check your internet connection", "Connection Failure", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }




        // ----------- The following 10 functions are resposible for checking blood availablility and decreasing its amount if found ----------
        private bool Check_blood_availability(string Hospital_Key, ref Dictionary<string, int> dic, string Blood_Type)
        {
            bool Isfound = false;
            if (Blood_Type == "ABPlus")
                Check_ABPlus(Hospital_Key, ref dic, ref Isfound);
            else if (Blood_Type == "ABMinus")
                Check_ABMinus(Hospital_Key, ref dic, ref Isfound);
            else if (Blood_Type == "APlus")
                Check_APlus(Hospital_Key, ref dic, ref Isfound);
            else if (Blood_Type == "AMinus")
                Check_AMinus(Hospital_Key, ref dic, ref Isfound);
            else if (Blood_Type == "BPlus")
                Check_BPlus(Hospital_Key, ref dic, ref Isfound);
            else if (Blood_Type == "BMinus")
                Check_BMinus(Hospital_Key, ref dic, ref Isfound);
            else if (Blood_Type == "OPlus")
                Check_OPlus(Hospital_Key, ref dic, ref Isfound);
            else
                Check_OMinus(Hospital_Key, ref dic, ref Isfound);
            return Isfound;
        }
        private void Check_ABPlus(string Hospital_Key, ref Dictionary<string, int> dic, ref bool Isfound)
        {
            if (!Isfound && dic["ABPlus"] >= 2)
            {
                Update_Blood_Values(Hospital_Key, "ABPlus", dic["ABPlus"] - 2);
                Isfound = true;
            }
            if (!Isfound && dic["OPlus"] >= 2)
            {
                Update_Blood_Values(Hospital_Key, "OPlus", dic["OPlus"] - 2);
                Isfound = true;
            }
            if (!Isfound && dic["APlus"] >= 2)
            {
                Update_Blood_Values(Hospital_Key, "APlus", dic["APlus"] - 2);
                Isfound = true;
            }
            if (!Isfound && dic["BPlus"] >= 2)
            {
                Update_Blood_Values(Hospital_Key, "BPlus", dic["BPlus"] - 2);
                Isfound = true;
            }
            if (!Isfound && dic["OMinus"] >= 2)
            {
                Update_Blood_Values(Hospital_Key, "OMinus", dic["OMinus"] - 2);
                Isfound = true;
            }
            if (!Isfound && dic["AMinus"] >= 2)
            {
                Update_Blood_Values(Hospital_Key, "AMinus", dic["AMinus"] - 2);
                Isfound = true;
            }
            if (!Isfound && dic["BMinus"] >= 2)
            {
                Update_Blood_Values(Hospital_Key, "BMinus", dic["BMinus"] - 2);
                Isfound = true;
            }
            if (!Isfound && dic["ABMinus"] >= 2)
            {
                Update_Blood_Values(Hospital_Key, "ABMinus", dic["ABMinus"] - 2);
                Isfound = true;
            }

        }
        private void Check_ABMinus(string Hospital_Key, ref Dictionary<string, int> dic, ref bool Isfound)
        {
            if (!Isfound && dic["ABMinus"] >= 2)
            {
                Update_Blood_Values(Hospital_Key, "ABMinus", dic["ABMinus"] - 2);
                Isfound = true;
            }
            if (!Isfound && dic["OMinus"] >= 2)
            {
                Update_Blood_Values(Hospital_Key, "OMinus", dic["OMinus"] - 2);
                Isfound = true;
            }
            if (!Isfound && dic["AMinus"] >= 2)
            {
                Update_Blood_Values(Hospital_Key, "AMinus", dic["AMinus"] - 2);
                Isfound = true;
            }
            if (!Isfound && dic["BMinus"] >= 2)
            {
                Update_Blood_Values(Hospital_Key, "BMinus", dic["BMinus"] - 2);
                Isfound = true;
            }
        }
        private void Check_APlus(string Hospital_Key, ref Dictionary<string, int> dic, ref bool Isfound)
        {
            if (!Isfound && dic["APlus"] >= 2)
            {
                Update_Blood_Values(Hospital_Key, "APlus", dic["APlus"] - 2);
                Isfound = true;
            }
            if (!Isfound && dic["OPlus"] >= 2)
            {
                Update_Blood_Values(Hospital_Key, "OPlus", dic["OPlus"] - 2);
                Isfound = true;
            }
            if (!Isfound && dic["OMinus"] >= 2)
            {
                Update_Blood_Values(Hospital_Key, "OMinus", dic["OMinus"] - 2);
                Isfound = true;
            }
            if (!Isfound && dic["AMinus"] >= 2)
            {
                Update_Blood_Values(Hospital_Key, "AMinus", dic["AMinus"] - 2);
                Isfound = true;
            }
        }
        private void Check_AMinus(string Hospital_Key, ref Dictionary<string, int> dic, ref bool Isfound)
        {
            if (!Isfound && dic["AMinus"] >= 2)
            {
                Update_Blood_Values(Hospital_Key, "AMinus", dic["AMinus"] - 2);
                Isfound = true;
            }
            if (!Isfound && dic["OMinus"] >= 2)
            {
                Update_Blood_Values(Hospital_Key, "OMinus", dic["OMinus"] - 2);
                Isfound = true;
            }
        }
        private void Check_BPlus(string Hospital_Key, ref Dictionary<string, int> dic, ref bool Isfound)
        {
            if (!Isfound && dic["BPlus"] >= 2)
            {
                Update_Blood_Values(Hospital_Key, "BPlus", dic["BPlus"] - 2);
                Isfound = true;
            }
            if (!Isfound && dic["OPlus"] >= 2)
            {
                Update_Blood_Values(Hospital_Key, "OPlus", dic["OPlus"] - 2);
                Isfound = true;
            }
            if (!Isfound && dic["OMinus"] >= 2)
            {
                Update_Blood_Values(Hospital_Key, "OMinus", dic["OMinus"] - 2);
                Isfound = true;
            }
            if (!Isfound && dic["BMinus"] >= 2)
            {
                Update_Blood_Values(Hospital_Key, "BMinus", dic["BMinus"] - 2);
                Isfound = true;
            }
        }
        private void Check_BMinus(string Hospital_Key, ref Dictionary<string, int> dic, ref bool Isfound)
        {
            if (!Isfound && dic["BMinus"] >= 2)
            {
                Update_Blood_Values(Hospital_Key, "BMinus", dic["BMinus"] - 2);
                Isfound = true;
            }
            if (!Isfound && dic["OMinus"] >= 2)
            {
                Update_Blood_Values(Hospital_Key, "OMinus", dic["OMinus"] - 2);
                Isfound = true;
            }
        }
        private void Check_OPlus(string Hospital_Key, ref Dictionary<string, int> dic, ref bool Isfound)
        {
            if (!Isfound && dic["OPlus"] >= 2)
            {
                Update_Blood_Values(Hospital_Key, "OPlus", dic["OPlus"] - 2);
                Isfound = true;
            }
            if (!Isfound && dic["OMinus"] >= 2)
            {
                Update_Blood_Values(Hospital_Key, "OMinus", dic["OMinus"] - 2);
                Isfound = true;
            }
        }
        private void Check_OMinus(string Hospital_Key, ref Dictionary<string, int> dic, ref bool Isfound)
        {
            if (!Isfound && dic["OMinus"] >= 2)
            {
                Update_Blood_Values(Hospital_Key, "OMinus", dic["OMinus"] - 2);
                Isfound = true;
            }
        }
        private async void Update_Blood_Values(string Hospital_Key, string Blood_type, int New_Value)
        {
            try
            {
                var UpdateData = new Dictionary<string, object>
                {
                    { Blood_type , New_Value }
                };
                await client.UpdateAsync($"CareConnect/HospitalData/{Hospital_Key}/", UpdateData);


                var UpdateDate = new Dictionary<string, object>
                {
                    { Blood_type+"_LastEdit" ,  DateTime.Now}
                };
                await client.UpdateAsync($"CareConnect/HospitalData/{Hospital_Key}/", UpdateDate);
            }
            catch (Exception)
            {
                MessageBox.Show("Failed to Update Data, Please check your internet connection", "Connection Failure", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void btnClear_Click(object sender, EventArgs e)
        {

            ConfigurationManager.AppSettings["StatusText"] = "";
            UpdateStatusTextBox();

        }

        private void BtnUpdate_Click(object sender, EventArgs e)
        {
            UpdateStatusTextBox();

        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (Hospital_number2 <= Hospital_number)
            {
                Porgress1.Enabled = false;
                Hospital_number2 += 10;
                int val = (Hospital_number2 * 100) / Hospital_number;
                if (val < 100) Porgress1.Value = 73;

                Porgress1.Text = Hospital_number2.ToString();
            }
           
           


        }

        private void label2_Click(object sender, EventArgs e)
        {
           this.Close();
        }

        private void Porgress1_Click(object sender, EventArgs e)
        {

        }




        public void RadForm()
        {
            // Form properties
            this.FormBorderStyle = FormBorderStyle.None; // Hide the default border
      //      this.BackColor = Color.White; // Set the form background color
            this.StartPosition = FormStartPosition.CenterScreen; // Center the form on the screen

         
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            // Create a path with rounded corners
            GraphicsPath path = new GraphicsPath();
            int arcWidth = borderRadius * 2;
            path.AddArc(0, 0, arcWidth, arcWidth, 180, 90);
            path.AddArc(this.Width - arcWidth, 0, arcWidth, arcWidth, 270, 90);
            path.AddArc(this.Width - arcWidth, this.Height - arcWidth, arcWidth, arcWidth, 0, 90);
            path.AddArc(0, this.Height - arcWidth, arcWidth, arcWidth, 90, 90);
            path.CloseAllFigures();

            // Set the form region to the path
            this.Region = new Region(path);
        }


    }
}
