using HPorvenir.Model;
using HPorvenir.User.DAL;
using System;
using System.Collections.Generic;
using System.Text;

namespace HPorvenir.Authentication
{
    public class AuthManager
    {
        readonly string key = "Digix.S.A.Gdl.Jalisco.Mx";
        UserDAL _userDal = null;

        public AuthManager(UserDAL userDal) {

            _userDal = userDal;
        }


        public string GenerateToken(Model.User user) {
            TokenManager token = new TokenManager();
            return token.GenerateToken(user);
        }


        public Model.User VerifyUser(string user, string password) {

            Model.User _user = null;
            
            try {
                _user = _userDal.GetUserByUserName(user);
                _user.Role = new[]{ "admin"};
            }
            catch (Exception ex) { 
            
            }


            if (password == Encryption.Decode(_user.Password, key))
            {
                return _user;
            }
            else { 
            //return incorrect password
            }

            return _user;
        }
    }
}
