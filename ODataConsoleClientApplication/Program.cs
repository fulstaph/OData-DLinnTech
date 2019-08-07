using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Xml;
using System.Xml.Linq;
using System.IO;


/* TASK: 
 
    Реализовать консольное приложение на С#, которое имеет три функции:
    получить список средств связи контакта; 
    добавить средство связи контакту; 
    удалить существующее средство связи контакта.
         
*/

namespace ODataConsoleClientApplication
{
    class Program
    {
        // Строка адреса BPM online сервиса OData
        private const string serverUri = "http://localhost:82/0/ServiceModel/EntityDataService.svc/";
        private const string authServiceUtri = "http://localhost:82/ServiceModel/AuthService.svc/Login";

        // Ссылки на пространства имен XML.
        private static readonly XNamespace ds = "http://schemas.microsoft.com/ado/2007/08/dataservices";
        private static readonly XNamespace dsmd = "http://schemas.microsoft.com/ado/2007/08/dataservices/metadata";
        private static readonly XNamespace atom = "http://www.w3.org/2005/Atom";
        static void Main(string[] args)
        {
            string contactName = "Георгий Темурович Боковиков";
            Guid contactId = GetGuidByContactName(contactName);
            string number = GetContactCommunicationsById(contactId);
            Console.WriteLine($"id: {contactId}, contact name: {contactName}, phone number: {number}");
            AddNewContactSkypeById(contactId, "100-1659281");
            DeleteContactCommunicationWebById(contactId);
            Console.ReadLine();
        }

        public static Guid GetGuidByContactName(string contactName)
        {
            string userName = "Supervisor", userPassword = "Supervisor";
            // Создание запроса на аутентификацию.
            var authRequest = HttpWebRequest.Create(authServiceUtri) as HttpWebRequest;
            authRequest.Method = "POST";
            authRequest.ContentType = "application/json";
            var bpmCookieContainer = new CookieContainer();
            // Включение использования cookie в запросе.
            authRequest.CookieContainer = bpmCookieContainer;
            // Получение потока, ассоциированного с запросом на аутентификацию.
            using (var requestStream = authRequest.GetRequestStream())
            {
                // Запись в поток запроса учетных данных пользователя BPMonline и дополнительных параметров запроса.
                using (var writer = new StreamWriter(requestStream))
                {
                    writer.Write(@"{
                                ""UserName"":""" + userName + @""",
                                ""UserPassword"":""" + userPassword + @""",
                                ""SolutionName"":""TSBpm"",
                                ""TimeZoneOffset"":-120,
                                ""Language"":""Ru-ru""
                                }");
                }
            }
            // Получение ответа от сервера. Если аутентификация проходит успешно, в объекте bpmCookieContainer будут 
            // помещены cookie, которые могут быть использованы для последующих запросов.
            using (var response = (HttpWebResponse)authRequest.GetResponse())
            {
                // Создание запроса на получение данных от сервиса OData.
                var dataRequest = HttpWebRequest.Create(serverUri + "ContactCollection?select=Id, Name")
                                        as HttpWebRequest;
                // Для получения данных используется HTTP-метод GET.
                dataRequest.Method = "GET";
                // Добавление полученных ранее аутентификационных cookie в запрос на получение данных.
                dataRequest.CookieContainer = bpmCookieContainer;
                // Получение ответа от сервера.
                using (var dataResponse = (HttpWebResponse)dataRequest.GetResponse())
                {
                    // Загрузка ответа сервера в xml-документ для дальнейшей обработки.
                    XDocument xmlDoc = XDocument.Load(dataResponse.GetResponseStream());
                    // Получение коллекции объектов контактов, соответствующих условию запроса.
                    var contacts = from entry in xmlDoc.Descendants(atom + "entry")
                                   select new
                                   {
                                       Id = new Guid(entry.Element(atom + "content")
                                                              .Element(dsmd + "properties")
                                                              .Element(ds + "Id").Value),
                                       Name = entry.Element(atom + "content")
                                                       .Element(dsmd + "properties")
                                                       .Element(ds + "Name").Value
                                   };
                    foreach (var contact in contacts)
                    {
                        if (contact.Name == contactName)
                            return contact.Id;
                    }
                }
            }

            return Guid.Empty;
        }
        public static string GetContactCommunicationsById(Guid contactId)
        {
            string userName =  "Supervisor", userPassword = "Supervisor";
            // Создание запроса на аутентификацию.
            // Формирование строки запроса к сервису.
            string requestUri = serverUri + "ContactCommunicationCollection/";
            // Создание объекта запроса к сервису.
            var request = HttpWebRequest.Create(requestUri) as HttpWebRequest;
            request.Method = "GET";
            request.Credentials = new NetworkCredential(userName, userPassword);
            using (var response = request.GetResponse())
            {
                // Получение ответа от сервиса в xml-формате.
                XDocument xmlDoc = XDocument.Load(response.GetResponseStream());
                // Получение коллекции объектов контактов, соответствующих условию запроса.
                var contacts = from entry in xmlDoc.Descendants(atom + "entry")
                               select new
                               {
                                   Id = new Guid(entry.Element(atom + "content")
                                                     .Element(dsmd + "properties")
                                                     .Element(ds + "Id").Value),
                                   ContactId = new Guid(entry.Element(atom + "content")
                                                                .Element(dsmd + "properties")
                                                                .Element(ds + "ContactId").Value
                                   ),
                                   Number = entry.Element(atom + "content")
                                               .Element(dsmd + "properties")
                                               .Element(ds + "Number").Value/*,
                                   Number = entry.Element(atom + "content")
                                               .Element(dsmd + "properties")
                                               .Element(ds + "Number").Value,*/
                                   // Инициализация свойств объекта, необходимых для дальнейшего использования.
                               };
                foreach (var contact in contacts)
                {
                    if (contact.ContactId == contactId)
                        return contact.Number;
                }
                return string.Empty;
            }

        }

        public static void AddNewContactSkypeById(Guid contactId, string skypeInfo = "")
        {
            var content = new XElement(dsmd + "properties",
                          new XElement(ds + "ContactId", contactId.ToString()),   
                          new XElement(ds + "CommunicationTypeId", "09E4BDA6-CFCB-DF11-9B2A-001D60E938C6"),
                          new XElement(ds + "Number", skypeInfo));
            var entry = new XElement(atom + "entry",
                        new XElement(atom + "content",
                        new XAttribute("type", "application/xml"), content));
            //Console.WriteLine(entry.ToString());
            var request = (HttpWebRequest)HttpWebRequest.Create(serverUri + "ContactCommunicationCollection/");
            request.Credentials = new NetworkCredential("Supervisor", "Supervisor");
            request.Method = "POST";
            request.Accept = "application/atom+xml";
            request.ContentType = "application/atom+xml;type=entry";
            using (var writer = XmlWriter.Create(request.GetRequestStream()))
            {
                entry.WriteTo(writer);
            }
            using (WebResponse response = request.GetResponse())
            {
                if (((HttpWebResponse)response).StatusCode == HttpStatusCode.Created)
                {
                    Console.WriteLine("New Skype contact has been added.");
                }
            }
        }

        public static void DeleteContactCommunicationWebById(Guid contactId)
        {
            // Id записи объекта, который необходимо удалить.
            // Создание запроса к сервису, который будет удалять данные.
            var request = (HttpWebRequest)HttpWebRequest.Create(serverUri
                    + "ContactCommunicationCollection(guid'" + "AEB822CC-7D65-408F-95C0-8BC34332C321" + "')");
            request.Credentials = new NetworkCredential("Supervisor", "Supervisor");
            request.Method = "DELETE";
            // Получение ответа от сервиса о результате выполненя операции.
            using (WebResponse response = request.GetResponse())
            {
                Console.WriteLine("Website entry has been successfully deleted.");
            }
        }
    }
}
