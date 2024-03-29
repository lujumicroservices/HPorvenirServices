﻿using System;
using System.Collections.Generic;
using System.Text;

namespace HPorvenir.Model
{
    public class User
    {
        public int Id { get; set; }
        public string UserName { get; set; }
        public string Name { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public string[] Role { 
            get {
                if (RoleArray == null)
                    return new string[] { };

                return RoleArray.Split(',');
            } 
        }

        public string Duration { get; set; }

        public string RoleArray { get; set; }
    }
}
