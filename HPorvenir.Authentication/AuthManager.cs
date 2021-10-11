using HPorvenir.Model;
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


        public bool UserExistsByUser(string userName) {
            try
            {
                return _userDal.ExistsUsers(userName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "error trying to get user");
                return false;
            }
        }

        public Model.User VerifyUser(string user, string password) {
            
            Model.User _user = null;

            _logger.LogDebug("getting user");
            try {
                _user = _userDal.GetUserByUserName(user);                
            }
            catch (Exception ex) {
                _logger.LogError(ex,"error trying to get user");
            }


            if (_user != null)
            {

                if (!string.IsNullOrEmpty(_user.Duration)) {
                    var duration = Convert.ToDateTime(_user.Duration);

                    if (DateTime.Now.CompareTo(duration) > 0) {
                        throw new Exception("La cuenta de usuario expiro");
                    }
                }

                _logger.LogDebug("validate process start");
                if (password == Encryption.Decode(_user.Password, key))
                {
                    return _user;
                }
                else {
                    _logger.LogError(@$"incorrect password for user {user}", user);
                }                
            }
            else {
                _logger.LogError(@$"user {user} does not exists", user);
            }


            throw new Exception("Usuario o Contraseña incorrecto");

            
        }

        public Model.User GetUset(string user)
        {

            Model.User _user = null;

            _logger.LogDebug("getting user");
            try
            {
                _user = _userDal.GetUserByUserName(user);                
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "error trying to get user");
            }


            return _user;
        }


        public List<Model.User> GetUsers() {

            var users = _userDal.GetUsers();
            foreach (var item in users) {
                try
                {
                    item.Password = Encryption.Decode(item.Password, key);
                }
                catch { 
                }                
            }

            return users;
        }

        public Model.User AddUsers(Model.User user)
        {
            try
            {
                user.Password = Encryption.Encode(user.Password, key);
            }
            catch { 
            
            }
            
            var id = _userDal.AddUsers(user);
            user.Id = id;
            return user;
        }


        public Model.User UpdateUsers(Model.User user)
        {
            user.Password = Encryption.Encode(user.Password, key);

            var id = _userDal.UpdateUsers(user);
            user.Id = id;
            return user;
        }

        public bool DeleteUsers(int id)
        {
            _userDal.DeleteUsers(id);
            return true;
        }

    }
}
