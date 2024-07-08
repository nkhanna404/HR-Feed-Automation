using System.Data;
using System.Collections.Generic;
using System;

namespace HRFeedApp
{
    public class HRFeeds
    {
        #region Properties
        public int OrganizationID { get; set; }
        public string FeedArgList { get; set; }
        public DateTime LastRunTime { get; set; }
        #endregion

        #region Constructors
        public HRFeeds() { }

        public HRFeeds(DataRow dr)
        {
            SetValues(dr);
        }
        #endregion

        #region Public Methods
        public void SetValues(DataRow dr)
        {
            OrganizationID = (int)dr["OrganizationID"];
            FeedArgList = dr["FeedArgList"].ToString();
            LastRunTime = dr["LastRunTime"] == DBNull.Value ? DateTime.MinValue : (DateTime)dr["LastRunTime"];
        }

        public static List<HRFeeds> GetAllFeeds()
        {
            List<HRFeeds> list = new List<HRFeeds>();
            DataView dv = getHRFeeds();
            foreach (DataRow dr in dv.Table.Rows)
            {
                HRFeeds hrFeedRow = new HRFeeds(dr);
                list.Add(hrFeedRow);
            }
            return list;
        }

        // Function to get all the autoreports from the database.
        private static DataView getHRFeeds()
        {
            string sql = "SELECT * FROM HRFeeds";
            DataView dv = Utility.GetDataFromQueryPortal(sql, CommandType.Text);
            return dv;
        }

        public static void Update(int orgID, DateTime lastRunTime)
        {
            string sql = "UPDATE HRFeeds SET LastRunTime = @LastRunTime "
                + " WHERE OrganizationID = @OrganizationID";

            var pars = new Dictionary<string, object>();
            pars.Add("@LastRunTime", lastRunTime);
            pars.Add("@OrganizationID", orgID);
            Utility.SQLNonQuery(sql, pars, false);
        }
        #endregion
    }
}
