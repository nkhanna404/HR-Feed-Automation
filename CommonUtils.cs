using System;
using System.Collections.Generic;
using System.Data;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;

namespace HRFeedApp
{
    public class CommonUtils
    {
        public static void LogMessageToFile(string msg, string path)
        {
            using (System.IO.StreamWriter sw = System.IO.File.AppendText(path))
            {
                string logLine = System.String.Format("{0:G}: {1}.", System.DateTime.Now, msg);
                sw.WriteLine(logLine);
            }
        }

        public static string generateEmailBody(string emailTemplate, string firstName, string lastName, string userName, string siteURL, string passwordResetURL, string orgCode, string orgName, string toEmail)
        {
            string emailBody = string.Format(emailTemplate, firstName, lastName, userName, siteURL, passwordResetURL, orgCode, orgName, toEmail);
            return emailBody;
        }
        public static bool sendAccessEmail(string strSubject, string toEmail, string strMessage)
        {
            string server = System.Configuration.ConfigurationManager.AppSettings["mailServer"];
            string fromAddress = System.Configuration.ConfigurationManager.AppSettings["fromEmail"];

            string account = System.Configuration.ConfigurationManager.AppSettings["mailAccount"];
            string accountpwd = System.Configuration.ConfigurationManager.AppSettings["mailPwd"];
            try
            {
                MailMessage message = new MailMessage(fromAddress, toEmail, strSubject, strMessage);
                message.Body = strMessage;

                message.IsBodyHtml = true;
                SmtpClient smtpClient = new SmtpClient(server);

                smtpClient.Port = Convert.ToInt32(System.Configuration.ConfigurationManager.AppSettings["SMTPPort"]);
                if (accountpwd != string.Empty)
                    smtpClient.Credentials = new System.Net.NetworkCredential(account, accountpwd);
                smtpClient.Send(message);
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        public static string formatPasswordRstLink(string siteURL, string passwordResetURL, int userID, string password)
        {
            passwordResetURL = passwordResetURL.Replace("(SiteURL)", siteURL);
            passwordResetURL = passwordResetURL.Replace("(USERID)", userID.ToString());
            passwordResetURL = passwordResetURL.Replace("(PASSWORD)", HashPwrd(password));
            return passwordResetURL;
        }

        public static string HashPwrd(string password)
        {
            byte[] bytes = (new ASCIIEncoding()).GetBytes(password);
            var encoder = new SHA1CryptoServiceProvider();
            return BitConverter.ToString(encoder.ComputeHash(bytes)).Replace("-", "");
        }

        public class OrgDetails
        {
            public string OrgName;
            public string OrgCode;
            public int DefaultAreaID;
        }

        public static OrgDetails GetOrgDetails(int orgID)
        {
            string sql = "SELECT Name, OrganizationCode, DefaultAreaID from Organization "
                + " WHERE OrganizationID = @OrganizationID";

            var pars = new Dictionary<string, object>();
            pars.Add("@OrganizationID", orgID);

            DataView dv = Utility.GetDataFromQueryPortal(sql, CommandType.Text, pars);
            if (dv.Table.Rows.Count > 0)
            {
                return new OrgDetails
                {
                    OrgName = dv.Table.Rows[0]["Name"].ToString(),
                    OrgCode = dv.Table.Rows[0]["OrganizationCode"].ToString(),
                    DefaultAreaID = Convert.ToInt32(dv.Table.Rows[0]["DefaultAreaID"])
                };
            }
            return null;
        }

        public static DateTime GetUserCreationDate(int userID)
        {
            string sql = "SELECT CreateDate from [dbo].[User] "
                + " WHERE UserID = @UserID";

            var pars = new Dictionary<string, object>();
            pars.Add("@UserID", userID);

            DataView dv = Utility.GetDataFromQueryPortal(sql, CommandType.Text, pars);
            if (dv.Table.Rows.Count > 0)
            {
                return Convert.ToDateTime(dv.Table.Rows[0]["CreateDate"]);
            }
            return DateTime.MinValue;
        }

        public class GroupDetails
        {
            public string GroupCode;
            public string GroupName;
        } 

        public static GroupDetails GetGroupDetails(int orgID, int groupID = 0, string groupCode = "", string groupTitle = "")
        {
            string sql = "SELECT Title, Code FROM Area WHERE ";
            var pars = new Dictionary<string, object>();
            if (groupID > 0)
            {
                sql += "AreaID = @AreaID";
                pars.Add("@AreaID", groupID);
            }
            else if (!string.IsNullOrEmpty(groupCode))
            {
                sql += "Code = @Code and OrganizationID = @OrganizationID";
                pars.Add("@OrganizationID", orgID);
                pars.Add("@Code", groupCode);
            }
            else if (!string.IsNullOrEmpty(groupTitle))
            {
                sql += "Title = @Title and OrganizationID = @OrganizationID";
                pars.Add("@OrganizationID", groupID);
                pars.Add("@Title", groupTitle);
            }          

            DataView dv = Utility.GetDataFromQueryPortal(sql, CommandType.Text, pars);
            if (dv.Table.Rows.Count > 0)
            {
                return new GroupDetails
                {
                    GroupName = dv.Table.Rows[0]["Title"].ToString(),
                    GroupCode = dv.Table.Rows[0]["Code"].ToString()
                };
            }
            return null;
        }

        public static GroupDetails CreateGroup(int orgID, string groupCode_or_Title)
        {
            string sql = "INSERT INTO Area (OrganizationID, Code, Title, IsDistrict) VALUES (@OrganizationID, @Code, @Title, @IsDistrict)";
            var pars = new Dictionary<string, object>();
            pars.Add("@OrganizationID", orgID);
            pars.Add("@Code", groupCode_or_Title);
            pars.Add("@Title", groupCode_or_Title);
            pars.Add("@IsDistrict", false);

            Utility.SQLNonQuery(sql, pars, false);

            // After creating, retrieve the group details to return
            return CommonUtils.GetGroupDetails(orgID, 0, groupCode_or_Title);
        }
    }
}
