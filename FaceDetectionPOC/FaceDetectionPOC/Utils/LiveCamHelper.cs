
using Microsoft.ProjectOxford.Face;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FaceDetectionPOC
{
    public static class LiveCamHelper
    {
        public static bool IsFaceRegistered { get; set; }

        public static bool IsInitialized { get; set; }

        public static string WorkspaceKey
        {
            get;
            set;
        }
        public static Action<string> GreetingsCallback { get => greetingsCallback; set => greetingsCallback = value; }

        private static Action<string> greetingsCallback;

        public static void Init(Action throttled = null)
        {
            FaceServiceHelper.ApiKey = "31f6feb7031f4d538409fb3074fcebf0";
            if (throttled != null)
                FaceServiceHelper.Throttled += throttled;

            WorkspaceKey = Guid.NewGuid().ToString();
            ImageAnalyzer.PeopleGroupsUserDataFilter = WorkspaceKey;
            FaceListManager.FaceListsUserDataFilter = WorkspaceKey;

            IsInitialized = true;
        }

        public static Task RegisterFaces()
        {
            return Task.Run( async() => {
                try { 
                    var persongroupId = Guid.NewGuid().ToString();
                    await FaceServiceHelper.CreatePersonGroupAsync(persongroupId,
                                                            "Xamarin",
                                                         WorkspaceKey);

                    foreach (var item in Workers.WORKES) {
                        var person = await FaceServiceHelper.CreatePersonAsync(persongroupId, item.Name);

                        item.IdFR = person.PersonId;

                        await FaceServiceHelper.AddPersonFaceAsync(persongroupId, person.PersonId,
                                                   item.Image, null, null);
                    }

                    await FaceServiceHelper.TrainPersonGroupAsync(persongroupId);
                    IsFaceRegistered = true;
                }
                catch(Exception e) {
                    Console.WriteLine(e.StackTrace);
                }

            });


        }

        //public static async Task RegisterFaces()
        //{

        //    try
        //    {
        //        var persongroupId = Guid.NewGuid().ToString();
        //        await FaceServiceHelper.CreatePersonGroupAsync(persongroupId,
        //                                                "Xamarin",
        //                                             WorkspaceKey);
        //        await FaceServiceHelper.CreatePersonAsync(persongroupId, "Albert Einstein");

        //        var personsInGroup = await FaceServiceHelper.GetPersonsAsync(persongroupId);

        //        await FaceServiceHelper.AddPersonFaceAsync(persongroupId, personsInGroup[0].PersonId,
        //                                                   "https://upload.wikimedia.org/wikipedia/commons/d/d3/Albert_Einstein_Head.jpg", null, null);

        //        await FaceServiceHelper.TrainPersonGroupAsync(persongroupId);


        //        IsFaceRegistered = true;


        //    }
        //    catch (FaceAPIException ex)

        //    {
        //        Console.WriteLine(ex.Message);
        //        IsFaceRegistered = false;

        //    }

        //}

        public static async Task ProcessCameraCapture(ImageAnalyzer e)
        {

            DateTime start = DateTime.Now;

            await e.DetectFacesAsync().ConfigureAwait(false);
            Console.WriteLine("Face Detected.");

            if (e.DetectedFaces != null && e.DetectedFaces.Any())
            {
                await e.IdentifyFacesAsync().ConfigureAwait(false);
                string greetingsText = GetGreettingFromFaces(e) + String.Format(" {0:ddd, MMMM dd yyyy h:mm tt}", DateTime.Now);

                if (e.IdentifiedPersons!= null && e.IdentifiedPersons.Any())
                {

                    if (greetingsCallback != null)
                    {
                        DisplayMessage(greetingsText);
                    }

                    Console.WriteLine(greetingsText);
                }
                else
                {
                    DisplayMessage("No Idea, who you're.. Register your face.");

                    Console.WriteLine("No Idea, who you're.. Register your face.");

                }
            }
            else
            {
                // DisplayMessage("No face detected.");

                Console.WriteLine("No Face ");

            }

            TimeSpan latency = DateTime.Now - start;
            var latencyString = string.Format("Face API latency: {0}ms", (int)latency.TotalMilliseconds);
            Console.WriteLine(latencyString);
        }

        private static string GetGreettingFromFaces(ImageAnalyzer img)
        {
            if (img.IdentifiedPersons!=null && img.IdentifiedPersons.Any())
            {
                string names = img.IdentifiedPersons.Count() > 1 ? string.Join(", ", img.IdentifiedPersons.Select(p => p.Person.Name)) : img.IdentifiedPersons.First().Person.Name;

                if (img.DetectedFaces.Count() > img.IdentifiedPersons.Count())
                {
                    return string.Format("Hi, {0} and company!", names);
                }
                else
                {
                    return string.Format("Hi, {0}!", names);
                }
            }
            else
            {
                if (img.DetectedFaces.Count() > 1)
                {
                    return "Hi everyone! If I knew any of you by name I would say it...";
                }
                else
                {
                    return "Hi there! If I knew you by name I would say it...";
                }
            }
        }

        static void DisplayMessage(string greetingsText)
        {
            greetingsCallback?.Invoke(greetingsText);
        }
    }
}
//