using Dapper;
using Dapper.Logging;
using Microsoft.Extensions.Configuration;
using System;
using System.Data;
using System.Data.SqlClient;

namespace HPorvenir.User.DAL
{
    public class UserDAL
    {
        IConfiguration _config = null;

        private readonly IDbConnectionFactory _connectionFactory;
        

        public UserDAL(IDbConnectionFactory connectionFactory,IConfiguration configuration) {
            _connectionFactory = connectionFactory;
        }

        public Model.User GetUserByUserName(string userName) {

            using (IDbConnection db = _connectionFactory.CreateConnection()) 
            {
                return db.QueryFirst<Model.User>("Select * From Users WHERE UserName=@userName", new { userName = userName } );
            }
        }


    }
}
