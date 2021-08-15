﻿using HPorvenir.Model;
using HPorvenir.User.DAL;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace HPorvenir.Authentication
{
    public class AuthManager
    {
        readonly string key = "Digix.S.A.Gdl.Jalisco.Mx";
        UserDAL _userDal = null;
        private readonly ILogger<AuthManager> _logger;


        public AuthManager(UserDAL userDal, ILogger<AuthManager> logger) {

            _userDal = userDal;
            _logger = logger;
        }


        public string GenerateToken(Model.User user) {
            TokenManager token = new TokenManager();
            return token.GenerateToken(user);
        }


        public Model.User VerifyUser(string user, string password) {
            
            Model.User _user = null;

            _logger.LogDebug("getting user");
            try {
                _user = _userDal.GetUserByUserName(user);
                _user.Role = new[]{ "admin"};
            }
            catch (Exception ex) {
                _logger.LogError(ex,"error trying to get user");
            }


            _logger.LogDebug("validate process start");
            if (password == Encryption.Decode(_user.Password, key))
            {
                return _user;
            }
            else { 
            //return incorrect password
            }

            return _user;
        }

        public Model.User GetUset(string user)
        {

            Model.User _user = null;

            _logger.LogDebug("getting user");
            try
            {
                _user = _userDal.GetUserByUserName(user);
                _user.Role = new[] { "admin" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "error trying to get user");
            }


            return _user;
        }
    }
}
