using System.Data;
using System.Data.SqlClient;
using System.Collections.Generic;
using System;

namespace HRFeedApp
{
    static class Utility
    {
        private static string _connectionStringPortal
        {
            get
            {
                return System.Configuration.ConfigurationManager.ConnectionStrings["LMSDBConnectPortal"].ToString();
            }
        }

        public static DataView GetDataFromQueryPortal(string query, CommandType command, Dictionary<string, object> parameters = null)
        {
            var ds = new DataSet();
            var da = new SqlDataAdapter();
            da.SelectCommand = new SqlCommand();
            da.SelectCommand.CommandTimeout = 1800;
            da.SelectCommand.CommandType = command;

            using (var conn = new SqlConnection(_connectionStringPortal))
            {
                da.SelectCommand.Connection = conn;
                da.SelectCommand.CommandText = query;

                if (parameters != null)
                {
                    foreach (string key in parameters.Keys)
                    {
                        da.SelectCommand.Parameters.AddWithValue(key, parameters[key]);
                    }
                }

                da.Fill(ds);
            }
            if (ds.Tables.Count > 0)
            {
                return ds.Tables[0].DefaultView;
            }

            return null;
        }

        public static int SQLNonQuery(string query, Dictionary<string, object> parameters, bool isStoredProcedure)
        {
            return SQLNonQuery(query, parameters, isStoredProcedure, out int _);
        }
        public static int SQLNonQuery(string query, Dictionary<string, object> parameters, bool isStoredProcedure, out int outputValue)
        {
            outputValue = 0; // Default value

            using (SqlConnection conn = new SqlConnection(_connectionStringPortal))
            {
                SqlCommand comm = new SqlCommand(query, conn);
                comm.CommandType = isStoredProcedure ? CommandType.StoredProcedure : CommandType.Text;

                foreach (string key in parameters.Keys)
                {
                    if (parameters[key] is SqlParameter sqlParam && sqlParam.Direction == ParameterDirection.Output)
                    {
                        comm.Parameters.Add(sqlParam);
                    }
                    else
                    {
                        if (parameters[key] == null)
                        {
                            comm.Parameters.AddWithValue(key, DBNull.Value);
                        }
                        else
                        {
                            comm.Parameters.AddWithValue(key, parameters[key]);
                        }
                    }
                }

                comm.CommandTimeout = 1800;
                conn.Open();
                comm.ExecuteNonQuery();

                // Retrieve the value of the output parameter
                foreach (SqlParameter param in comm.Parameters)
                {
                    if (param.Direction == ParameterDirection.Output)
                    {
                        outputValue = (int)param.Value;
                        break;
                    }
                }
            }

            return outputValue;

        }
    }
}
