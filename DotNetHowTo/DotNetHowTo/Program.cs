﻿using System;
using System.Configuration;
using System.Linq;
using System.Threading;
using Microsoft.Azure.Search;
using Microsoft.Azure.Search.Models;
using Microsoft.Spatial;

namespace AzureSearch.SDKHowTo
{
    class Program
    {
        // This sample shows how to delete, create, upload documents and query an index
        static void Main(string[] args)
        {
            string searchServiceName = ConfigurationManager.AppSettings["SearchServiceName"];
            string apiKey = ConfigurationManager.AppSettings["SearchServiceApiKey"];

            SearchServiceClient serviceClient = new SearchServiceClient(searchServiceName, new SearchCredentials(apiKey));

            Console.WriteLine("{0}", "Deleting index...\n");
            DeleteHotelsIndexIfExists(serviceClient);

            Console.WriteLine("{0}", "Creating index...\n");
            CreateHotelsIndex(serviceClient);

            Console.WriteLine("{0}", "Uploading documents...\n");
            UploadDocuments(serviceClient);

            SearchIndexClient indexClient = new SearchIndexClient(searchServiceName, "hotels", new SearchCredentials(apiKey));

            SearchParameters parameters;
            DocumentSearchResult<Hotel> results;

            Console.WriteLine("Search the entire index for the term 'budget' and return only the hotelName field:\n");

            parameters = 
                new SearchParameters() 
                { 
                    Select = new[] { "hotelName" }
                };
            
            results = indexClient.Documents.Search<Hotel>("budget", parameters);
            
            WriteDocuments(results);

            Console.Write("Apply a filter to the index to find hotels cheaper than $150 per night, ");
            Console.WriteLine("and return the hotelId and description:\n");

            parameters =
                new SearchParameters()
                {
                    Filter = "baseRate lt 150",
                    Select = new[] { "hotelId", "description" }
                };

            results = indexClient.Documents.Search<Hotel>("*", parameters);

            WriteDocuments(results);

            Console.Write("Search the entire index, order by a specific field (lastRenovationDate) ");
            Console.Write("in descending order, take the top two results, and show only hotelName and ");
            Console.WriteLine("lastRenovationDate:\n");

            parameters =
                new SearchParameters()
                {
                    OrderBy = new[] { "lastRenovationDate desc" },
                    Select = new[] { "hotelName", "lastRenovationDate" },
                    Top = 2
                };

            results = indexClient.Documents.Search<Hotel>("*", parameters);

            WriteDocuments(results);

            Console.WriteLine("Search the entire index for the term 'motel':\n");

            parameters = new SearchParameters();
            results = indexClient.Documents.Search<Hotel>("motel", parameters);

            WriteDocuments(results);

            Console.WriteLine("{0}", "Complete.  Press any key to end application...\n");
            Console.ReadKey();
        }

        private static void DeleteHotelsIndexIfExists(SearchServiceClient serviceClient)
        {
            if (serviceClient.Indexes.Exists("hotels"))
            {
                serviceClient.Indexes.Delete("hotels");
            }
        }

        private static void CreateHotelsIndex(SearchServiceClient serviceClient)
        {
            var definition = new Index()
            {
                Name = "hotels",
                Fields = new[] 
                { 
                    new Field("hotelId", DataType.String)                       { IsKey = true, IsFilterable = true },
                    new Field("baseRate", DataType.Double)                      { IsFilterable = true, IsSortable = true, IsFacetable = true },
                    new Field("description", DataType.String)                   { IsSearchable = true },
                    new Field("description_fr", AnalyzerName.FrLucene),
                    new Field("hotelName", DataType.String)                     { IsSearchable = true, IsFilterable = true, IsSortable = true },
                    new Field("category", DataType.String)                      { IsSearchable = true, IsFilterable = true, IsSortable = true, IsFacetable = true },
                    new Field("tags", DataType.Collection(DataType.String))     { IsSearchable = true, IsFilterable = true, IsFacetable = true },
                    new Field("parkingIncluded", DataType.Boolean)              { IsFilterable = true, IsFacetable = true },
                    new Field("smokingAllowed", DataType.Boolean)               { IsFilterable = true, IsFacetable = true },
                    new Field("lastRenovationDate", DataType.DateTimeOffset)    { IsFilterable = true, IsSortable = true, IsFacetable = true },
                    new Field("rating", DataType.Int32)                         { IsFilterable = true, IsSortable = true, IsFacetable = true },
                    new Field("location", DataType.GeographyPoint)              { IsFilterable = true, IsSortable = true }
                }
            };

            serviceClient.Indexes.Create(definition);
        }

        private static void UploadDocuments(SearchServiceClient serviceClient)
        {
            SearchIndexClient indexClient = serviceClient.Indexes.GetClient("hotels");

            var actions =
                new IndexAction<Hotel>[]
                {
                    IndexAction.Upload(
                        new Hotel()
                        { 
                            HotelId = "1", 
                            BaseRate = 199.0, 
                            Description = "Best hotel in town",
                            DescriptionFr = "Meilleur hôtel en ville",
                            HotelName = "Fancy Stay",
                            Category = "Luxury", 
                            Tags = new[] { "pool", "view", "wifi", "concierge" },
                            ParkingIncluded = false, 
                            SmokingAllowed = false,
                            LastRenovationDate = new DateTimeOffset(2010, 6, 27, 0, 0, 0, TimeSpan.Zero), 
                            Rating = 5, 
                            Location = GeographyPoint.Create(47.678581, -122.131577)
                        }),
                    IndexAction.Upload(
                        new Hotel()
                        { 
                            HotelId = "2", 
                            BaseRate = 79.99,
                            Description = "Cheapest hotel in town",
                            DescriptionFr = "Hôtel le moins cher en ville",
                            HotelName = "Roach Motel",
                            Category = "Budget",
                            Tags = new[] { "motel", "budget" },
                            ParkingIncluded = true,
                            SmokingAllowed = true,
                            LastRenovationDate = new DateTimeOffset(1982, 4, 28, 0, 0, 0, TimeSpan.Zero),
                            Rating = 1,
                            Location = GeographyPoint.Create(49.678581, -122.131577)
                        }),
                    IndexAction.MergeOrUpload(
                        new Hotel() 
                        { 
                            HotelId = "3", 
                            BaseRate = 129.99,
                            Description = "Close to town hall and the river"
                        }),
                    IndexAction.Delete(new Hotel() { HotelId = "6" })
                };

            var batch = IndexBatch.New(actions);

            try
            {
                indexClient.Documents.Index(batch);
            }
            catch (IndexBatchException e)
            {
                // Sometimes when your Search service is under load, indexing will fail for some of the documents in
                // the batch. Depending on your application, you can take compensating actions like delaying and
                // retrying. For this simple demo, we just log the failed document keys and continue.
                Console.WriteLine(
                    "Failed to index some of the documents: {0}",
                    String.Join(", ", e.IndexingResults.Where(r => !r.Succeeded).Select(r => r.Key)));
            }

            Console.WriteLine("Waiting for documents to be indexed...\n");
            Thread.Sleep(2000);
        }

        private static void WriteDocuments(DocumentSearchResult<Hotel> searchResults)
        {
            foreach (SearchResult<Hotel> result in searchResults.Results)
            {
                Console.WriteLine(result.Document);
            }

            Console.WriteLine();
        }
    }
}
