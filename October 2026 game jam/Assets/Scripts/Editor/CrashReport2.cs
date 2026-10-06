/*
log: C:\Users\james\AppData\Local\Unity\Editor\Editor-prev.log exists=True modified=2026-10-06 09:45:29Z
total lines: 82392
---- matching lines (last 80) ----
BatchMode: 0, IsHumanControllingUs: 1, StartBugReporterOnCrash: 1, Is64bit: 1
[D3D12 Device Filter] Vendor Name: NVIDIA
[D3D12 Device Filter] Device Name: NVIDIA GeForce RTX 4050 Laptop GPU
[D3D12 Device Filter] Driver Version: 32.0.15.8186
[D3D12 Device Filter] Feature Level: 12.2
[D3D12 Device Filter] Graphics Memory: 5921 MB
[D3D12 Device Filter] Processor Count: 16
[D3D12 Device Filter] Device Type: Discrete
GfxDevice: creating device client; kGfxThreadingModeSplitJobs
Domain Reload Profiling: 850ms
Domain Reload Profiling: 2749ms
Android Extension - Scanning For ADB Devices 227 ms
Android Extension - Scanning For ADB Devices 245 ms
Android Extension - Scanning For ADB Devices 293 ms
Android Extension - Scanning For ADB Devices 219 ms
[Worker2] Start importing Assets/Scripts/Editor/CrashReport.cs using Guid(6d327b70347ed11428767c32eb0217dd) Importer(2089858483,b0f066a214c2f5e87bad3c948f4d605d) 
StackOverflowException: The requested operation caused a stack overflow.
UnityEngine.DebugLogHandler:Internal_LogException_Injected(Exception, IntPtr)
UnityEngine.DebugLogHandler:Internal_LogException(Exception, Object)
UnityEngine.DebugLogHandler:LogException(Exception, Object)
UnityEngine.Logger:LogException(Exception, Object)
UnityEngine.Debug:LogException(Exception)
UnityEngine.<>c:<RegisterUECatcher>b__0_0(Object, UnhandledExceptionEventArgs)
---- last 40 lines (excluding module list) ----
  at Newtonsoft.Json.Serialization.JsonSerializerInternalWriter.SerializeValue (Newtonsoft.Json.JsonWriter writer, System.Object value, Newtonsoft.Json.Serialization.JsonContract valueContract, Newtonsoft.Json.Serialization.JsonProperty mem...
  at Newtonsoft.Json.Serialization.JsonSerializerInternalWriter.SerializeObject (Newtonsoft.Json.JsonWriter writer, System.Object value, Newtonsoft.Json.Serialization.JsonObjectContract contract, Newtonsoft.Json.Serialization.JsonProperty m...
  at Newtonsoft.Json.Serialization.JsonSerializerInternalWriter.SerializeValue (Newtonsoft.Json.JsonWriter writer, System.Object value, Newtonsoft.Json.Serialization.JsonContract valueContract, Newtonsoft.Json.Serialization.JsonProperty mem...
  at Newtonsoft.Json.Serialization.JsonSerializerInternalWriter.SerializeObject (Newtonsoft.Json.JsonWriter writer, System.Object value, Newtonsoft.Json.Serialization.JsonObjectContract contract, Newtonsoft.Json.Serialization.JsonProperty m...
  at Newtonsoft.Json.Serialization.JsonSerializerInternalWriter.SerializeValue (Newtonsoft.Json.JsonWriter writer, System.Object value, Newtonsoft.Json.Serialization.JsonContract valueContract, Newtonsoft.Json.Serialization.JsonProperty mem...
  at Newtonsoft.Json.Serialization.JsonSerializerInternalWriter.SerializeObject (Newtonsoft.Json.JsonWriter writer, System.Object value, Newtonsoft.Json.Serialization.JsonObjectContract contract, Newtonsoft.Json.Serialization.JsonProperty m...
  at Newtonsoft.Json.Serialization.JsonSerializerInternalWriter.SerializeValue (Newtonsoft.Json.JsonWriter writer, System.Object value, Newtonsoft.Json.Serialization.JsonContract valueContract, Newtonsoft.Json.Serialization.JsonProperty mem...
  at Newtonsoft.Json.Serialization.JsonSerializerInternalWriter.SerializeObject (Newtonsoft.Json.JsonWriter writer, System.Object value, Newtonsoft.Json.Serialization.JsonObjectContract contract, Newtonsoft.Json.Serialization.JsonProperty m...
  at Newtonsoft.Json.Serialization.JsonSerializerInternalWriter.SerializeValue (Newtonsoft.Json.JsonWriter writer, System.Object value, Newtonsoft.Json.Serialization.JsonContract valueContract, Newtonsoft.Json.Serialization.JsonProperty mem...
  at Newtonsoft.Json.Serialization.JsonSerializerInternalWriter.SerializeObject (Newtonsoft.Json.JsonWriter writer, System.Object value, Newtonsoft.Json.Serialization.JsonObjectContract contract, Newtonsoft.Json.Serialization.JsonProperty m...
  at Newtonsoft.Json.Serialization.JsonSerializerInternalWriter.SerializeValue (Newtonsoft.Json.JsonWriter writer, System.Object value, Newtonsoft.Json.Serialization.JsonContract valueContract, Newtonsoft.Json.Serialization.JsonProperty mem...
  at Newtonsoft.Json.Serialization.JsonSerializerInternalWriter.SerializeObject (Newtonsoft.Json.JsonWriter writer, System.Object value, Newtonsoft.Json.Serialization.JsonObjectContract contract, Newtonsoft.Json.Serialization.JsonProperty m...
  at Newtonsoft.Json.Serialization.JsonSerializerInternalWriter.SerializeValue (Newtonsoft.Json.JsonWriter writer, System.Object value, Newtonsoft.Json.Serialization.JsonContract valueContract, Newtonsoft.Json.Serialization.JsonProperty mem...
  at Newtonsoft.Json.Serialization.JsonSerializerInternalWriter.SerializeObject (Newtonsoft.Json.JsonWriter writer, System.Object value, Newtonsoft.Json.Serialization.JsonObjectContract contract, Newtonsoft.Json.Serialization.JsonProperty m...
  at Newtonsoft.Json.Serialization.JsonSerializerInternalWriter.SerializeValue (Newtonsoft.Json.JsonWriter writer, System.Object value, Newtonsoft.Json.Serialization.JsonContract valueContract, Newtonsoft.Json.Serialization.JsonProperty mem...
  at Newtonsoft.Json.Serialization.JsonSerializerInternalWriter.Serialize (Newtonsoft.Json.JsonWriter jsonWriter, System.Object value, System.Type objectType) [0x00047] in <761cf2a144514d2291a678c334d49e9b>:0 
  at Newtonsoft.Json.JsonSerializer.SerializeInternal (Newtonsoft.Json.JsonWriter jsonWriter, System.Object value, System.Type objectType) [0x0023a] in <761cf2a144514d2291a678c334d49e9b>:0 
  at Newtonsoft.Json.JsonSerializer.Serialize (Newtonsoft.Json.JsonWriter jsonWriter, System.Object value) [0x00000] in <761cf2a144514d2291a678c334d49e9b>:0 
  at Newtonsoft.Json.Linq.JToken.FromObjectInternal (System.Object o, Newtonsoft.Json.JsonSerializer jsonSerializer) [0x0001c] in <761cf2a144514d2291a678c334d49e9b>:0 
  at Newtonsoft.Json.Linq.JToken.FromObject (System.Object o, Newtonsoft.Json.JsonSerializer jsonSerializer) [0x00000] in <761cf2a144514d2291a678c334d49e9b>:0 
  at CodeMaestro.UnityMcp.Editor.Helpers.GameObjectSerializer.CreateTokenFromValue (System.Object value, System.Type type) [0x0000a] in <f51ab536df2146df807eb2c1b527b823>:0 
  at CodeMaestro.UnityMcp.Editor.Helpers.GameObjectSerializer.AddSerializableValue (System.Collections.Generic.Dictionary`2[TKey,TValue] dict, System.String name, System.Type type, System.Object value) [0x0000d] in <f51ab536df2146df807eb2c1...
  at CodeMaestro.UnityMcp.Editor.Helpers.GameObjectSerializer.GetComponentData (UnityEngine.Component c, System.Boolean includeNonPublicSerializedFields) [0x00ac4] in <f51ab536df2146df807eb2c1b527b823>:0 
  at CodeMaestro.UnityMcp.Editor.Tools.ManageGameObject.GetComponentsFromTarget (System.String target, System.String searchMethod, System.Boolean includeNonPublicSerialized) [0x00093] in <f51ab536df2146df807eb2c1b527b823>:0 
  at CodeMaestro.UnityMcp.Editor.Tools.ManageGameObject.HandleCommand (Newtonsoft.Json.Linq.JObject params) [0x005eb] in <f51ab536df2146df807eb2c1b527b823>:0 
  at CodeMaestro.UnityMcp.Editor.McpCommandExecutor.ExecuteCommand (CodeMaestro.UnityMcp.Editor.Models.Command command) [0x0039a] in <f51ab536df2146df807eb2c1b527b823>:0 
  at CodeMaestro.UnityMcp.Editor.McpBridgeServer.ExecuteCommand (CodeMaestro.UnityMcp.Editor.Models.Command command) [0x00027] in <f51ab536df2146df807eb2c1b527b823>:0 
  at CodeMaestro.UnityMcp.Editor.McpBridgeServer.ProcessCommands () [0x000c1] in <f51ab536df2146df807eb2c1b527b823>:0 
  at UnityEditor.EditorApplication.Internal_CallUpdateFunctions () [0x00032] in <6dd00658fb454e6fb4c06a416ab8eaa1>:0 
UnityEngine.DebugLogHandler:Internal_LogException_Injected(Exception, IntPtr)
UnityEngine.DebugLogHandler:Internal_LogException(Exception, Object)
UnityEngine.DebugLogHandler:LogException(Exception, Object)
UnityEngine.Logger:LogException(Exception, Object)
UnityEngine.Debug:LogException(Exception)
UnityEngine.<>c:<RegisterUECatcher>b__0_0(Object, UnhandledExceptionEventArgs)

Checking for leaked weakptr:
  Found no leaked weakptrs.
All object instances wasn't destroyed before shutting down

*/
