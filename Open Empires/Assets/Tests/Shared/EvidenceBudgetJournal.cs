using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OpenEmpires.TestSupport
{
    // Test assembly only, never a gameplay authority or a player spending policy.
    [Serializable] public sealed class EvidenceBudgetSnapshot
    {
        public string schema,run_id,accounting;
        public int semantic_http_limit,semantic_initial_http,semantic_repair_http;
        public int live_submission_limit,live_submissions;
        public double cloud_asr_billable_seconds_limit,cloud_asr_billable_seconds;
        public int cloud_asr_requests;
        public EvidenceReservation[] transactions;
    }
    [Serializable] public sealed class EvidenceReservation
    {
        public string id,kind,utc,status,category;
        public double billable_seconds;
        public int http_status;
        public double audio_duration_seconds,minimum_billable_seconds,billing_quantum_seconds;
        public string billing_basis_reference;
    }
    public sealed class EvidenceBudgetJournal
    {
        public const int MaximumBytes=65536,MaximumRecords=256;
        private readonly string path,run;
        private static readonly string[] Fields={"schema","run_id","accounting","semantic_http_limit","semantic_initial_http","semantic_repair_http","live_submission_limit","live_submissions","cloud_asr_billable_seconds_limit","cloud_asr_billable_seconds","cloud_asr_requests","transactions"};
        public EvidenceBudgetJournal(string path,string runId,bool createIfMissing)
        {
            if(!Guid.TryParseExact(runId,"D",out var identity)||identity==Guid.Empty)throw Failure();
            this.path=Path.GetFullPath(path);run=runId;
            if(Path.GetFileName(this.path)!="paid-usage-ledger.json")throw Failure();
            Directory.CreateDirectory(Path.GetDirectoryName(this.path));CheckPaths();
            Locked(()=>
            {
                if(!File.Exists(this.path))
                {
                    if(!createIfMissing)throw Failure();
                    Write(new EvidenceBudgetSnapshot{schema="openempires-grand-fix-paid-budget@2",run_id=run,
                        semantic_http_limit=6,live_submission_limit=6,cloud_asr_billable_seconds_limit=600,
                        transactions=Array.Empty<EvidenceReservation>()});
                }
                Read();return 0;
            });
        }
        public EvidenceBudgetSnapshot Snapshot()=>Locked(Read);
        public string Reserve(string kind,double billableSeconds)
        {
            if(!KnownKind(kind)||double.IsNaN(billableSeconds)||double.IsInfinity(billableSeconds)
                ||(kind=="cloud-asr"?billableSeconds<=0||billableSeconds>600:billableSeconds!=0))throw Failure();
            return Locked(()=>
            {
                var ledger=Read();
                if(ledger.transactions.Length>=MaximumRecords)throw Failure("budget-exhausted");
                if(kind=="submission")
                {if(ledger.live_submissions>=Math.Min(6,ledger.live_submission_limit))throw Failure("budget-exhausted");ledger.live_submissions++;}
                else if(kind=="cloud-asr")
                {
                    if(ledger.cloud_asr_billable_seconds+billableSeconds>Math.Min(600,ledger.cloud_asr_billable_seconds_limit))throw Failure("budget-exhausted");
                    ledger.cloud_asr_requests++;ledger.cloud_asr_billable_seconds+=billableSeconds;
                }
                else
                {
                    if(ledger.semantic_initial_http+ledger.semantic_repair_http>=Math.Min(6,ledger.semantic_http_limit))throw Failure("budget-exhausted");
                    if(kind=="semantic-initial")ledger.semantic_initial_http++;else ledger.semantic_repair_http++;
                }
                string id=Guid.NewGuid().ToString("D");
                var list=new List<EvidenceReservation>(ledger.transactions){new EvidenceReservation{id=id,kind=kind,
                    utc=DateTime.UtcNow.ToString("O"),status="reserved",category="unknown",billable_seconds=billableSeconds}};
                ledger.transactions=list.ToArray();Write(ledger);return id;
            });
        }
        public void Complete(string id,int status,string category)
        {
            if(status<0||status>599||!KnownCategory(category))throw Failure();
            Locked(()=>
            {
                var ledger=Read();var entry=ledger.transactions.FirstOrDefault(t=>t.id==id);
                if(entry==null||entry.status!="reserved")throw Failure();
                entry.status="completed";entry.http_status=status;entry.category=category;Write(ledger);return 0;
            });
        }
        public void RecordAudioDetails(string id,double duration,double minimum,double quantum,string basis)
        {
            if(!Finite(duration)||duration<.1||duration>60||!Finite(minimum)||minimum<=0||minimum>600
                ||!Finite(quantum)||quantum<=0||quantum>600||!ValidBasis(basis))throw Failure();
            Locked(()=>
            {
                var ledger=Read();var entry=ledger.transactions.FirstOrDefault(t=>t.id==id);
                if(entry==null||entry.kind!="cloud-asr"||entry.status!="reserved"
                    ||entry.billable_seconds<Math.Max(minimum,Math.Ceiling(duration/quantum)*quantum))throw Failure();
                entry.audio_duration_seconds=duration;entry.minimum_billable_seconds=minimum;
                entry.billing_quantum_seconds=quantum;entry.billing_basis_reference=basis;Write(ledger);return 0;
            });
        }
        private T Locked<T>(Func<T> action)
        {
            try
            {
                CheckPaths();
                // A leftover regular file is not a live lock; OS ownership is. No
                // PID guessing, forced lock removal, waiting loop or reset API.
                using(var lease=new FileStream(path+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None))return action();
            }
            catch(Exception error)when(error is IOException||error is UnauthorizedAccessException||error is JsonException||error is FormatException||error is OverflowException||error is DecoderFallbackException)
            {throw Failure();}
        }
        private void CheckPaths()
        {
            for(string cursor=Path.GetDirectoryName(path);cursor!=null;cursor=Path.GetDirectoryName(cursor))
                if(Directory.Exists(cursor)&&(File.GetAttributes(cursor)&FileAttributes.ReparsePoint)!=0)throw Failure();
            foreach(string file in new[]{path,path+".lock"})
                if(File.Exists(file)&&(File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)throw Failure();
        }
        private EvidenceBudgetSnapshot Read()
        {
            using(var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                if(file.Length>MaximumBytes)throw Failure();
                using(var bytes=new MemoryStream())
                {
                    var chunk=new byte[4096];int read;
                    while((read=file.Read(chunk,0,chunk.Length))>0)
                    {if(bytes.Length+read>MaximumBytes)throw Failure();bytes.Write(chunk,0,read);}
                    string text=new UTF8Encoding(false,true).GetString(bytes.ToArray());
                    JObject root;
                    using(var reader=new JsonTextReader(new StringReader(text)){MaxDepth=8,DateParseHandling=DateParseHandling.None})
                    {
                        root=JObject.Load(reader,new JsonLoadSettings{DuplicatePropertyNameHandling=DuplicatePropertyNameHandling.Error});
                        if(reader.Read())throw Failure();
                    }
                    if(root.Properties().Any(p=>!Fields.Contains(p.Name))||Fields.Any(f=>root.Property(f)==null))throw Failure();
                    var ledger=root.ToObject<EvidenceBudgetSnapshot>(JsonSerializer.Create(new JsonSerializerSettings{MissingMemberHandling=MissingMemberHandling.Error}));
                    Validate(ledger);return ledger;
                }
            }
        }
        private void Validate(EvidenceBudgetSnapshot ledger)
        {
            if(ledger==null||ledger.run_id!=run||(ledger.schema!="openempires-grand-fix-paid-budget@1"&&ledger.schema!="openempires-grand-fix-paid-budget@2")
                ||ledger.transactions==null||ledger.transactions.Length>MaximumRecords
                ||ledger.semantic_http_limit<0||ledger.semantic_http_limit>6||ledger.live_submission_limit<0||ledger.live_submission_limit>6
                ||!Finite(ledger.cloud_asr_billable_seconds_limit)||ledger.cloud_asr_billable_seconds_limit<0||ledger.cloud_asr_billable_seconds_limit>600)throw Failure();
            var ids=new HashSet<string>(StringComparer.Ordinal);int submissions=0,initial=0,repair=0,asr=0;double seconds=0;
            foreach(var item in ledger.transactions)
            {
                if(item==null||!Guid.TryParseExact(item.id,"D",out var id)||id==Guid.Empty||!ids.Add(item.id)||!KnownKind(item.kind)
                    ||item.utc==null||item.utc.Length>40||!DateTimeOffset.TryParse(item.utc,out _)
                    ||(item.status!="reserved"&&item.status!="completed")||!KnownCategory(item.category)
                    ||item.http_status<0||item.http_status>599||!Finite(item.billable_seconds))throw Failure();
                if(item.kind=="submission")submissions++;
                else if(item.kind=="semantic-initial")initial++;
                else if(item.kind=="semantic-repair")repair++;
                else{if(item.billable_seconds<=0||item.billable_seconds>600)throw Failure();asr++;seconds+=item.billable_seconds;}
                if(item.kind!="cloud-asr"&&item.billable_seconds!=0)throw Failure();
                if(item.billing_basis_reference!=null&&(item.kind!="cloud-asr"||!ValidBasis(item.billing_basis_reference)
                    ||!Finite(item.audio_duration_seconds)||item.audio_duration_seconds<.1||item.audio_duration_seconds>60
                    ||!Finite(item.minimum_billable_seconds)||item.minimum_billable_seconds<=0||item.minimum_billable_seconds>600
                    ||!Finite(item.billing_quantum_seconds)||item.billing_quantum_seconds<=0||item.billing_quantum_seconds>600
                    ||item.billable_seconds<Math.Max(item.minimum_billable_seconds,Math.Ceiling(item.audio_duration_seconds/item.billing_quantum_seconds)*item.billing_quantum_seconds)))throw Failure();
            }
            if(ledger.live_submissions!=submissions||ledger.semantic_initial_http!=initial||ledger.semantic_repair_http!=repair
                ||ledger.cloud_asr_requests!=asr||!Finite(ledger.cloud_asr_billable_seconds)||ledger.cloud_asr_billable_seconds!=seconds
                ||submissions>ledger.live_submission_limit||initial+repair>ledger.semantic_http_limit||seconds>ledger.cloud_asr_billable_seconds_limit)throw Failure();
        }
        private void Write(EvidenceBudgetSnapshot ledger)
        {
            ledger.schema="openempires-grand-fix-paid-budget@2";
            ledger.accounting="Conservative run-wide reservations at the attempt boundary; unfinished/unknown attempts remain consumed. No raw requests, headers or audio. No reset API.";
            Validate(ledger);
            byte[] bytes=new UTF8Encoding(false,true).GetBytes(JsonConvert.SerializeObject(ledger,Formatting.Indented));
            if(bytes.Length>MaximumBytes)throw Failure();
            string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                using(var file=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                {file.Write(bytes,0,bytes.Length);file.Flush(true);}
                if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);
            }
            finally{if(File.Exists(temporary))File.Delete(temporary);}
        }
        private static bool Finite(double value)=>!double.IsNaN(value)&&!double.IsInfinity(value);
        private static bool ValidBasis(string value)=>value!=null&&System.Text.RegularExpressions.Regex.IsMatch(value,@"^[a-z0-9][a-z0-9-]{0,59}$")&&!value.StartsWith("sk-");
        private static bool KnownKind(string kind)=>kind=="submission"||kind=="semantic-initial"||kind=="semantic-repair"||kind=="cloud-asr";
        private static bool KnownCategory(string category)=>category=="unknown"||category=="response"||category=="cancelled"||category=="network-error"||category=="response-limit"||category=="attempt-error";
        private static InvalidOperationException Failure(string code="budget-unavailable")
        {
            var error=new InvalidOperationException("Evidence budget unavailable, invalid, locked or exhausted; no new paid attempt is permitted.");
            error.Data["evidence_guard"]=code;return error;
        }
    }
}
