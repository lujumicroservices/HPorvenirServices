using Dapper;
using Dapper.Logging;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace HPorvenir.User.DAL
{
    public class UserDAL
    {
        IConfiguration _config = null;

        private readonly IDbConnectionFactory _connectionFactory;
        

        public UserDAL(IDbConnectionFactory connectionFactory,IConfiguration configuration) {
            _connectionFactory = connectionFactory;
        }

        public Model.User GetUserByUserName(string userName) 
        {
            using (IDbConnection db = _connectionFactory.CreateConnection()) 
            {                                
                return db.QueryFirstOrDefault<Model.User>("Select * From Users WHERE UserName=@userName", new { userName = userName } );
            }
        }

        public List<Model.User> GetUsers()
        {
            using (IDbConnection db = _connectionFactory.CreateConnection())
            {
                return db.Query<Model.User>("Select * From Users").ToList();
            }
        }

        public bool ExistsUsers(string userName)
        {
            using (IDbConnection db = _connectionFactory.CreateConnection())
            {
                string sql = "SELECT count(*) FROM Users WHERE userName = @userName";
                return db.ExecuteScalar<int>(sql, new { userName = userName }) == 1;
            }
        }


        public int AddUsers(Model.User user)
        {         
            using (IDbConnection db = _connectionFactory.CreateConnection())
            {
                string sql = "INSERT INTO Users (Email, LastName, Name, Password, UserName, RoleArray, Duration) VALUES (@Email, @LastName, @Name, @Password, @UserName,@RoleArray,@Duration)";                
                return db.Execute(sql, user);
            }
        }

        public int UpdateUsers(Model.User user)
        {         
            using (IDbConnection db = _connectionFactory.CreateConnection())
            {
                string sql = "UPDATE Users set  Email = @Email, LastName = @LastName, Name = @Name, Password = @Password, UserName = @UserName, RoleArray = @RoleArray, Duration = @Duration WHERE Id = @Id";
                return db.Execute(sql, user);
            }
        }

        public int DeleteUsers(int Id)
        {            
            using (IDbConnection db = _connectionFactory.CreateConnection())
            {
                string sql = "DELETE Users WHERE Id = @Id";
                return db.Execute(sql, new { Id = Id });
            }
        }


    }
}
