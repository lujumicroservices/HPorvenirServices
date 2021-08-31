using HPorvenir.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace HPorvenir.Web.Api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [Authorize]
    public class MailController : Controller
    {
        [HttpPost]
        [AllowAnonymous]
        public IActionResult Index(Contact contact)
        {
            CreateTestMessage2(contact);
            return Ok(true);
        }


        public static void CreateTestMessage2(Contact contact)
        {
            string to = "s_perdiendo@msn.com";
            string from = "ben@contoso.com";
            MailMessage message = new MailMessage(from, to);
            message.Subject = "contacto hemeroteca porvenir";
            message.IsBodyHtml = true;
            message.Body = $@"

                <style>
                    contact-header:{{
                        background-color:#32AFF2
                    }}    
                    
                    contact-title:{{
                        color:white
                    }}    
        

                </style>
                <div style='background-color:#32AFF2;height: 80px;margin-bottom: 20px;'>
                                 <h1 style='color: white;padding-bottom: 30px;padding-top: 30px;padding-left: 15px;'>Solicitud Hemeroteca Porvenir</h1>
                </div>

                <div class='contact-body'>
                    <table>
                        <tbody>
                        <tr>
                        <td>Empresa :</td>
                        <td>{contact.Business}</td>
                        </tr>
                        <tr>
                        <td>Nombre :</td>
                        <td>{contact.Name}</td>
                        </tr>
                        <tr>
                        <td>Apellido :</td>
                        <td>{contact.LastName}</td>
                        </tr>
                        <tr>
                        <td>Telefono :</td>
                        <td>{contact.Phone}</td>
                        </tr>
                        <tr>
                        <td>Correo :</td>
                        <td>{contact.Email}</td>
                        </tr>
                        <tr>
                        <td>Ciudad :</td>
                        <td>{contact.City}</td>
                        </tr>
                        <tr>
                        <td>Estado :</td>
                        <td>{contact.State}</td>
                        </tr>
                        <tr>
                        <td>Pais :</td>
                        <td>{contact.Country}</td>
                        </tr>
                        <tr>
                        <td>Tipo de Cuenta :</td>
                        <td>{contact.AccountType}</td>
                        </tr>
                        <tr>
                        <td>Tipo de Busqueda :</td>
                        <td>{contact.SearchType}</td>
                        </tr>
                        <tr>
                        <td>Commentarios :</td>
                        <td>{contact.Comments}</td>
                        </tr>
                        </tbody>
                    </table>
            </div>


";
            

            var client = new SmtpClient
            {
                Host = "smtp.gmail.com",
                Port = 587,
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential("hemerotecaporvenir@gmail.com", "rexndxpvbnmbjsfe")
            };

            // Credentials are necessary if the server requires the client
            // to authenticate before it will send email on the client's behalf.
            //client.UseDefaultCredentials = true;

            try
            {
                client.Send(message);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception caught in CreateTestMessage2(): {0}",
                    ex.ToString());
            }
        }
    }
}
