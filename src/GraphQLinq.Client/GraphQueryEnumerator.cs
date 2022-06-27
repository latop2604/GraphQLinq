using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;

namespace GraphQLinq
{
    class GraphQueryEnumerator<T, TSource> : IEnumerator<T>
    {
        private IEnumerator<T> listEnumerator;
        private JsonDocument jsonDocument;

        private readonly string query;
        private readonly string baseUrl;
        private readonly string authorization;
        private readonly QueryType queryType;
        private readonly Func<TSource, T> mapper;
        private readonly JsonSerializerOptions jsonSerializerOptions;

        private const string DataPathPropertyName = "data";
        private const string ErrorPathPropertyName = "errors";

        internal GraphQueryEnumerator(GraphContext context, string query, QueryType queryType, Func<TSource, T> mapper)
        {
            this.query = query;
            this.mapper = mapper;
            this.queryType = queryType;
            baseUrl = context.BaseUrl;
            jsonSerializerOptions = context.JsonSerializerOptions ?? new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
            };
            authorization = context.Authorization;
        }

        public void Dispose()
        {
            listEnumerator?.Dispose();
            jsonDocument?.Dispose();
        }

        public bool MoveNext()
        {
            if (listEnumerator == null)
            {
                var data = DownloadData();
                jsonDocument = data.Item2;
                listEnumerator = data.Item1.GetEnumerator();
            }

            return listEnumerator.MoveNext();
        }

        private (IEnumerable<T>, JsonDocument) DownloadData()
        {
            var json = DownloadJson();

            var jsonDocument = JsonDocument.Parse(json);

            if (jsonDocument.RootElement.TryGetProperty(ErrorPathPropertyName, out var errorElement) && errorElement.ValueKind != JsonValueKind.Null)
            {
                var errorElementRawText = errorElement.GetRawText();
                var errors = JsonSerializer.Deserialize<List<GraphQueryError>>(errorElementRawText);
                throw new GraphQueryExecutionException(errors, query);
            }

            var dataElement = jsonDocument.RootElement.GetProperty(DataPathPropertyName);
            var resultElement = dataElement.GetProperty(GraphQueryBuilder<T>.ResultAlias);
            var enumerable = (resultElement.ValueKind == JsonValueKind.Array
                ? (IEnumerable<JsonElement>)resultElement.EnumerateArray()
                : new List<JsonElement> { resultElement }
                )
                .Select(jToken =>
                {
                    if (mapper != null)
                    {
                        var result = JsonSerializer.Deserialize<TSource>(jToken.GetRawText(), jsonSerializerOptions);
                        return mapper.Invoke(result);
                    }

                    var r = JsonSerializer.Deserialize<T>(jToken.GetRawText(), jsonSerializerOptions);
                    return r;
                });

            return (enumerable, jsonDocument);
        }

        private string DownloadJson()
        {
            WebRequest.DefaultWebProxy.Credentials = CredentialCache.DefaultCredentials;

            using (var webClient = new WebClient { Proxy = WebRequest.DefaultWebProxy })
            {
                webClient.Headers.Add(HttpRequestHeader.ContentType, "application/json");

                if (!string.IsNullOrEmpty(authorization))
                {
                    webClient.Headers.Add(HttpRequestHeader.Authorization, authorization);
                }

                try
                {
                    return webClient.UploadString(baseUrl, query);
                }
                catch (WebException exception)
                {
                    using (var responseStream = exception.Response?.GetResponseStream())
                    {
                        using (var streamReader = new StreamReader(responseStream))
                        {
                            return streamReader.ReadToEnd();
                        }
                    }
                }
            }
        }

        [ExcludeFromCodeCoverage]
        public void Reset()
        {
            throw new NotImplementedException();
        }

        public T Current => listEnumerator.Current;

        [ExcludeFromCodeCoverage]
        object IEnumerator.Current => Current;
    }
}