using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CsvHelper;
using CsvHelper.Configuration;
using System.Data.SqlClient;
using System.Data;

namespace HRFeedApp
{
    public class HRFeedModel
    {
        public class HRFeedAppArgs
        {
            public List<string> csv_headers { get; set; }
            public string temp_pwd { get; set; }
            public bool send_access_email { get; set; }
            public AccessEmailArgs access_email_args { get; set; }
            public CSVFileArgs csv_file_args { get; set; }
        }

        public class AccessEmailArgs
        {
            public string access_email_subject { get; set; }
            public string access_email_message { get; set; }
            public string access_email_siteURL { get; set; }
            public string access_email_pwdResetURL {  get; set; }
        }

        public class CSVFileArgs
        {
            public string csv_file_location { get; set;}
            public string csv_file_name { get; set;}
        }

        public static HRFeedAppArgs DeserializeHRFeedAppArgs(string argListString)
        {
            HRFeedAppArgs args = Newtonsoft.Json.JsonConvert.DeserializeObject<HRFeedAppArgs>(argListString);
            return args;
        }
    }
    public class FeedCSVProcessing
    {
        // Function to process one row from the HRFeed table at a time.
        public static void ProcessFeed(HRFeeds feed)
        {
            // Deserialize the argument list for each feed row.
            HRFeedModel.HRFeedAppArgs feedArgs = HRFeedModel.DeserializeHRFeedAppArgs(feed.FeedArgList);

            // Complete CSV filepath from feed argument list.
            string filePath = feedArgs.csv_file_args.csv_file_location + feedArgs.csv_file_args.csv_file_name;

            // Check if a new modified file exists in the file location.
            bool newFileExists = checkForNewFile(filePath, feed.LastRunTime);
            if (newFileExists)
            {
                // Read the CSV from the file location and name of the file as read from the feed argument list.
                ReadCSV(filePath, feed.OrganizationID, feed.LastRunTime, feedArgs);

                // Update the LastRunTime to keep track of when the feed needs to run next.
                HRFeeds.Update(feed.OrganizationID, DateTime.Now);
            }          
        }
        public static void ReadCSV(string filePath, int orgID, DateTime feedLastRunTime, HRFeedModel.HRFeedAppArgs feedArgs)
        {
            string feedLog = System.Configuration.ConfigurationManager.AppSettings["feedLog"];
            var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
            };

            using (var reader = new StreamReader(filePath))
            using (var csv = new CsvReader(reader, csvConfig))
            {
                var records = new List<Dictionary<string, object>>();
                var headers = new List<string>();

                // Start reading the feed.
                csv.Read();

                // Making sure the feed's header is read only once.
                csv.ReadHeader();
                headers.AddRange(csv.HeaderRecord);

                // Loop to traverse through the CSV and add each column to a dynamic object.
                while (csv.Read())
                {
                    // Dictionary to hold the Header and the matching value based on the header for each csv row.
                    var record = new Dictionary<string, object>();
                    string field;

                    // Loop through all the headers and then record the actual value in the row to the dictionary.
                    foreach (var header in headers)
                    {
                        field = csv.GetField(header);
                        record.Add(header, field);
                    }
                    records.Add(record);
                }

                // Loop through each row in the records list and assign the value's to the appropriate arguments that will be used to create the user.
                foreach (var record in records)
                {
                    var userName = record.ContainsKey("UserName") ? record["UserName"].ToString() : string.Empty;
                    var firstName = record.ContainsKey("FirstName") ? record["FirstName"].ToString() : string.Empty;
                    var lastName = record.ContainsKey("LastName") ? record["LastName"].ToString() : string.Empty;
                    var email = record.ContainsKey("Email") ? record["Email"].ToString() : string.Empty;
                    var employeeID = record.ContainsKey("EmployeeID") ? record["EmployeeID"].ToString() : string.Empty;
                    var groupCode = record.ContainsKey("GroupCode") ? record["GroupCode"].ToString() : string.Empty;
                    var groupTitle = record.ContainsKey("GroupTitle") ? record["GroupTitle"].ToString() : string.Empty;
                    var jobCode = record.ContainsKey("JobCode") ? record["JobCode"].ToString() : null;
                    var jobTitle = record.ContainsKey("JobTitle") ? record["JobTitle"].ToString() : null;
                    var jobAssignDate = record.ContainsKey("JobAssignDate") ? Convert.ToDateTime(record["JobAssignDate"]) : DateTime.Now;
                    var hrisImportDate = record.ContainsKey("HRISImportDate") ? Convert.ToDateTime(record["HRISImportDate"]) : DateTime.Now;

                    if (string.IsNullOrEmpty(userName))
                    {
                        continue;
                    }

                    // try catch to avoid any error in one row of the csv to break the entire csv from being processed.
                    try
                    {
                        // Get organization details (Name, OrgCode and DefaultAreaID) to be used in the access email and if group details are not present in csv.
                        CommonUtils.OrgDetails orgDetails = CommonUtils.GetOrgDetails(orgID);
                        CommonUtils.GroupDetails groupDetails = null;

                        if (!string.IsNullOrEmpty(groupCode) && !string.IsNullOrEmpty(groupTitle))
                        {
                            // Do Nothing
                        }
                        else
                        {
                            // Grab details for the default group to be used for creating a user.
                            if (string.IsNullOrEmpty(groupCode) && string.IsNullOrEmpty(groupTitle))
                            {
                                groupDetails = CommonUtils.GetGroupDetails(orgID, orgDetails.DefaultAreaID);
                            }
                            // Grab details for the existing group using the groupCode.
                            else if (string.IsNullOrEmpty(groupCode))
                            {
                                groupDetails = CommonUtils.GetGroupDetails(orgID, 0, string.Empty, groupTitle);
                            }
                            // Grab details for the existing group using the groupTitle.
                            else if (string.IsNullOrEmpty(groupTitle))
                            {
                                groupDetails = CommonUtils.GetGroupDetails(orgID, 0, groupCode, string.Empty);
                            }

                            // Create a new group using only the groupcode.
                            if (groupDetails == null && !string.IsNullOrEmpty(groupCode))
                            {
                                groupDetails = CommonUtils.CreateGroup(orgID, groupCode);
                            }
                            // Create a new group using only the grouptitle.
                            else if (groupDetails == null && !string.IsNullOrEmpty(groupTitle))
                            {
                                groupDetails = CommonUtils.CreateGroup(orgID, groupTitle);
                            }

                            if (groupDetails != null)
                            {
                                groupCode = groupDetails.GroupCode;
                                groupTitle = groupDetails.GroupName;
                            }
                            
                        }                        

                        int userID = createOrUpdateUserFromCSV(userName, CommonUtils.HashPwrd(feedArgs.temp_pwd), firstName, lastName, email, employeeID, groupCode, groupTitle, jobCode, jobTitle, jobAssignDate, orgID, hrisImportDate);

                        DateTime userCreationDate = CommonUtils.GetUserCreationDate(userID);

                        // Send Access email only for newly created users, to avoid resending the email to the same users.
                        if (feedArgs.send_access_email && (userCreationDate > feedLastRunTime))
                        {
                            // Password reset URL for the new users access email.                            
                            string passwordResetURL = CommonUtils.formatPasswordRstLink(feedArgs.access_email_args.access_email_siteURL, feedArgs.access_email_args.access_email_pwdResetURL, userID, feedArgs.temp_pwd);                          

                            // Compose the access email message using the template as given in the HRFeeds argument list.
                            string emailBody = CommonUtils.generateEmailBody(feedArgs.access_email_args.access_email_message, firstName, lastName, userName, feedArgs.access_email_args.access_email_siteURL, passwordResetURL, orgDetails.OrgCode, orgDetails.OrgName, email);

                            // Once the email body is generated, send the access email to the learner.
                            if (emailBody != string.Empty)
                            {
                                CommonUtils.sendAccessEmail(feedArgs.access_email_args.access_email_subject, email, emailBody);
                            }                          
                        }
                    }
                    catch (Exception ex)
                    {
                        CommonUtils.LogMessageToFile(ex.Message, feedLog);
                    }
                    
                }
            }
        }


        private static int createOrUpdateUserFromCSV(string userName, string password, string firstName, string lastName, string email, string employeeID, string groupCode, string groupTitle, string jobCode, string jobTitle, DateTime jobAssignDate, int orgID, DateTime hrisImportDate)
        {
            string sql = "spAddUserFromAPI";
            var pars = new Dictionary<string, object>();

            pars.Add("@userName", userName);
            pars.Add("@password", password);
            pars.Add("@firstName", firstName);
            pars.Add("@lastName", lastName);
            pars.Add("@employeeID", employeeID);
            pars.Add("@Email", email);
            pars.Add("@areaCode", groupCode);
            pars.Add("@areaTitle", groupTitle);
            pars.Add("@JobCode", jobCode);
            pars.Add("@JobTitle", jobTitle);
            pars.Add("@JobAssignDate", jobAssignDate);
            pars.Add("@OrganizationID", orgID);
            pars.Add("@HRISImportDate", hrisImportDate);

            pars.Add("@userID", new SqlParameter("@userID", SqlDbType.Int) { Direction = ParameterDirection.Output });

            int userID = Utility.SQLNonQuery(sql, pars, true, out int outputValue);
            return outputValue;
        }

        private static bool checkForNewFile(string filePath, DateTime feedLastRuntime)
        {
            if (!File.Exists(filePath))
            {
                // Do nothing.
            }
            else
            {
                System.IO.FileInfo fileInfo = new System.IO.FileInfo(filePath);
                DateTime modifiedDate = fileInfo.LastWriteTime;
                // Check for the last modified date on the file.
                // If it's greater than the last time the file ran then there is a new file in the location to be run.
                if (modifiedDate > feedLastRuntime)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
