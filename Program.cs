using System;
using System.Collections.Generic;

namespace HRFeedApp
{
    class Program
    {
        public static void Main(string[] args)
        {
            // Log to catch any errors while processing a feed.
            string feedLog = System.Configuration.ConfigurationManager.AppSettings["feedLog"];
            // Get all the HRFeeds from the database. If any row exists then continue on to read & process the file.
            List<HRFeeds> feeds = HRFeeds.GetAllFeeds();
            if (feeds.Count > 0 )
            {
                // Loop through all the rows returned from the HRFeeds table.
                foreach (HRFeeds feed in feeds)
                {
                    try
                    {
                        CommonUtils.LogMessageToFile(System.DateTime.Now + " , Start Processing CSV for Organization with ID: " + feed.OrganizationID, feedLog);
                        // Function to Process each feed.
                        FeedCSVProcessing.ProcessFeed(feed);
                    }
                    catch (Exception ex)
                    {
                        // Log any error to a text file at the time of processing the feed.
                        CommonUtils.LogMessageToFile(ex.Message, feedLog);
                    }
                }
            }
        }
    }
}
