using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Android.App;
using Android.OS;
using Android.Support.V7.App;
using Android.Runtime;
using Android.Widget;
using Java.Lang.Reflect;
using Java.Net;
using Newtonsoft.Json;
using NotesOnAndroid;
using Environment = System.Environment;

namespace Notes_Android
{
    public class Note
    {
        public int id;
        public string content;
        public DateTime time;
        public TimeSpan duration;
        public List<int> tags;
    }

    public class NotesBackendConnector
    {
        private HttpClient client;
        private string URL;
        private ILogger log;

        public NotesBackendConnector(ILogger logger, string pass)
        {
            log = logger;
            client = new HttpClient();
            URL = "https://notesbackend.k0haku.space/b/api/notes/";
            client.DefaultRequestHeaders.Add("cool-token", pass);
        }

        public async Task<Note> CreateNote(string content, List<ENoteTags> tags)
        {
            List<int> newTags = tags.ConvertAll(e => (int)e);
            var note = new Note() { time = DateTime.Now.ToLocalTime(), content = content, tags = newTags};
            var jsonContent = JsonConvert.SerializeObject(note);
            log.Log($"[Note] creating '{content}'...");
            var resp =  await client.PostAsync(URL, new StringContent(jsonContent, Encoding.UTF8, "application/json"));
            var resContent = resp.Content.ReadAsStringAsync().Result;
            log.Log($"[Note] '{content}' " + (resp.StatusCode == HttpStatusCode.Created ? "succeeded" : "failed:\n" + resContent));
            var resNote = JsonConvert.DeserializeObject<Note>(resContent);
            return resNote;
        }

        public async Task<string> GetStuff()
        {
            var resp = await client.GetAsync(URL + "?date=2020-01-17&tags=work-end");
            string status = resp.StatusCode.ToString();
            string result = resp.Content.ReadAsStringAsync().Result;
            return status + " - " + result;
        }

        public async Task<Note> UpdateNote(string content, Note lastBreakNote)
        {
            var jsonContent = JsonConvert.SerializeObject(lastBreakNote);
            log.Log($"[Note] patching '{content}'...");
            var resp = await client.PatchAsync(URL + lastBreakNote.id + "/", new StringContent(jsonContent, Encoding.UTF8, "application/json"));
            var resContent = resp.Content.ReadAsStringAsync().Result;
            log.Log($"[Note] '{content}' " + (resp.StatusCode == HttpStatusCode.OK ? "succeeded" : "failed:\n" + resContent));
            var resNote = JsonConvert.DeserializeObject<Note>(resContent);
            return resNote;
        }
    }

    public interface ILogger
    {
        void Log(string msg);
    }

    public enum ENoteTags
    {
        Break = 3,
        Work = 21,
        WorkStart = 62,
        WorkEnd = 65,
    }

    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = true)]
    public class MainActivity : AppCompatActivity, ILogger
    {
        private TextView workText;
        private TextView logText;
        private NotesBackendConnector conn;

        private Button btnStartWork;
        private Button btnStopWork;
        
        private Button btnStartBreak;
        private Button btnStopBreak;
        private Note lastBreakNote = null;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.activity_main);

            workText = FindViewById<TextView>(Resource.Id.textView2);
            logText = FindViewById<TextView>(Resource.Id.textView3);
            logText.Text = "";
            btnStartWork = FindViewById<Button>(Resource.Id.button5);
            btnStartWork.Click += StartWork;
            btnStopWork = FindViewById<Button>(Resource.Id.button6);
            btnStopWork.Click += StopWork;
            btnStartBreak = FindViewById<Button>(Resource.Id.button);
            btnStartBreak.Click += StartBreak;
            btnStopBreak = FindViewById<Button>(Resource.Id.button2);
            btnStopBreak.Click += StopBreak;

            string path = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            string filename = Path.Combine(path, "config.txt");
            string pass;
            using (var reader = new StreamReader(filename))
            {
                pass = reader.ReadToEnd();
                Log(pass);
            }
            conn = new NotesBackendConnector(this, pass);
        }

        private async void StopBreak(object sender, EventArgs e)
        {
            if (lastBreakNote == null)
            {
                Log($"!Please start break first!");
                return;
            }

            lastBreakNote.duration = DateTime.Now - lastBreakNote.time;
            var newNote = await conn.UpdateNote("break end", lastBreakNote);
            Log("Pressed stop break");
            lastBreakNote = null;
        }

        private async void StartBreak(object sender, EventArgs e)
        {
            if (lastBreakNote != null)
            {
                Log($"!Please end break first!");
                return;
            }
            
            var note = await conn.CreateNote("break start", new List<ENoteTags>() {ENoteTags.Work, ENoteTags.Break});
            lastBreakNote = note;
        }

        private async void StopWork(object sender, EventArgs eventArgs)
        {
            await conn.CreateNote("work end", new List<ENoteTags>() {ENoteTags.Work, ENoteTags.WorkEnd});            
        }

        private async void StartWork(object sender, EventArgs eventArgs)
        {
            await conn.CreateNote("work start", new List<ENoteTags>() {ENoteTags.Work, ENoteTags.WorkStart});
        }

        public void Log(string msg)
        {
            // if (logText.Text.Count(c => c.Equals('\n')) + 1 > 30)
            //     logText.Text = logText.Text.Remove(0, logText.Text.Length / 2);
            logText.Text = msg + "\n" + logText.Text;
        }
    }
}