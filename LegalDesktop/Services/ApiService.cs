﻿using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace LegalDesktop.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;

        public ApiService()
        {
            _httpClient = new HttpClient();
        }

        public async Task<(int success, string token)> Login(string username, string password)
        {
            // Cambiamos el objeto de loginData para que use "email" y "password"
            var loginData = new
            {
                email = username,
                password = password
            };

            var json = JsonSerializer.Serialize(loginData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

         var response = await _httpClient.PostAsync("https://localhost:7067/api/Users/login", content);
            // var response = await _httpClient.PostAsync("https://cncivil04.pjn.gov.ar/legaltrack/api/Users/login", content);

            if (response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ApiResponse>(responseBody);

                // Verificamos que la respuesta no sea nula y que el statusCode sea 200
                if (result != null && result.statusCode == 200)
                {
                    if (result.data.roleUser.name != "president" &&  result.data.roleUser.name != "vice")
                    {
                        System.Windows.MessageBox.Show("Usuario o contraseña invalida.", "Estas en el lugar correcto? Grabando ip y hora...", MessageBoxButton.OK, MessageBoxImage.Error);
                       return (0, null);
                    }
                    return (1, result.data.token); // Retorna éxito y el token
                }
            }

            return (0, null); // Retorna fallo
        }

        // Clase para deserializar la respuesta de la API

        private class ApiResponse
        {
            public int statusCode { get; set; }
            public ApiData data { get; set; }
            public string message { get; set; }
        }

        private class ApiData
        {
            public int id { get; set; } // Asegúrate de incluir el Id
            public string email { get; set; } // Asegúrate de incluir el Email
            public string token { get; set; } // Asegúrate de incluir el Token
            public string tokenExpiration { get; set; }

            public RoleUser roleUser { get; set; }
        }
        private class RoleUser
        {
            public int id { get; set; }

            public string name { get; set; }

        }
    }
}