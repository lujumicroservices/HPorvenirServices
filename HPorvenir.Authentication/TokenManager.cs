using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace HPorvenir.Authentication
{
    public class TokenManager
    {

		public string GenerateToken(Model.User user)
		{
			var mySecret = "asdv234234^&%&^%&^hjsdfb2%%%";
			var mySecurityKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(mySecret));

			var myIssuer = "http://hemerotecaporvenir.com";
			var myAudience = "http://hemerotecaporvenir.com";

			var tokenHandler = new JwtSecurityTokenHandler();


			List<Claim> claims = new List<Claim>();
			claims.Add(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));
			claims.Add(new Claim(ClaimTypes.Name, user.UserName));
			claims.Add(new Claim(ClaimTypes.Email, user.Email));
			claims.Add(new Claim(ClaimTypes.GivenName, user.Name + " " + user.LastName));

			foreach (var rol in user.Role) {
				claims.Add(new Claim(ClaimTypes.Role, rol.ToLower()));
			}



			var tokenDescriptor = new SecurityTokenDescriptor
			{
				Subject = new ClaimsIdentity(claims.ToArray()),
				Expires = DateTime.UtcNow.AddDays(7),
				Issuer = myIssuer,
				Audience = myAudience,
				SigningCredentials = new SigningCredentials(mySecurityKey, SecurityAlgorithms.HmacSha256Signature)
			};

			var token = tokenHandler.CreateToken(tokenDescriptor);
			return tokenHandler.WriteToken(token);
		}


		


	

	}
}
