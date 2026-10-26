using System;
using OneWayDps;
class HistoryJsonTests {
    static int Main(){try{Run();return 0;}catch(Exception error){Console.Error.WriteLine(error.Message);return 1;}}
    static void Run(){
        var data=new HistoryDatabase();var engine=new HistorySession(data);var run=engine.Start("0:a","荒原",0,0,"2026-10-25T00:00:00Z",false);
        run.Add(new RunSource{Key="device:1",Name="短弓",Kind="weapon"},new RunSource{Key="device:2",Name="爆炸箭袋",Kind="auxiliary"},50,100);engine.Checkpoint("save-file-hash");
        string json=HistoryJson.Encode(data);
        if(!json.Contains("\"Runs\"") || !json.Contains("\"Checkpoints\"") || !json.Contains("\"Sources\""))throw new Exception("Nested history data missing");
        if(json.Contains("\"GlobalDps\"") || json.Contains("$type"))throw new Exception("Only stable fields should be saved");
        var restored=HistoryJson.Decode(json);if(restored.Runs.Count!=1 || restored.Checkpoints.Count!=1 || restored.Runs[0].Sources.Count!=2)throw new Exception("Nested history roundtrip failed");
        if(restored.Runs[0].Damage!=50 || restored.Runs[0].Sources[1].Name!="爆炸箭袋")throw new Exception("Damage or localized name lost");
        var continued=new HistorySession(restored);if(!continued.Resume("save-file-hash","later") || continued.Current.Damage!=50)throw new Exception("Saved history cannot resume");
        bool failed=false;try{HistoryJson.Decode(json+"{\"Schema\":1}");}catch{failed=true;}if(!failed)throw new Exception("Trailing document must be rejected");
        Console.WriteLine("PASS production history JSON: nested runs/sources/checkpoints, damage, Chinese names, resume, fields-only format and malformed input");
    }
}
