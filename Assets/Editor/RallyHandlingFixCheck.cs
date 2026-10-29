using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class RallyHandlingFixCheck
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    const string Report = "Logs/HandlingFix_20261029/check.txt";
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    public static void Run()
    {
        File.WriteAllText(Report, "Handling/UI verification (Editor, no Player build)\n");
        Check(!EditorUtility.scriptCompilationFailed, "Compilation failed");
        foreach (int index in new[] { 1, 2, 3 })
        {
            EditorSceneManager.OpenScene($"Assets/Scenes/Circuit_0{index}.unity");
            var car = Object.FindObjectsByType<JrsVehicleController>().Single(c => c.name == RallyGameSession.VehicleName);
            var dynamics = Object.FindObjectsByType<RallyVehicleDynamics>().Single(d => d.VehicleController == car);
            Check(car.maxSteerAngle == 55 && dynamics.SteeringResponse == 96, "Steering mismatch");
            dynamics.ApplySetup();
            var body = car.GetComponent<Rigidbody>();
            Check(body.interpolation == RigidbodyInterpolation.Interpolate, "Body interpolation disabled");
            var calculate = typeof(RallyVehicleDynamics).GetMethod("CalculateAutomaticBrakeAmount", Private);
            foreach (float speed in new[] { 30f, 60f, 90f, 120f })
                foreach (float turn in new[] { 0f, .25f, .5f, .6f })
                    Check((float)calculate.Invoke(dynamics, new object[] { turn, speed }) == 0, "Normal curve brakes the car");
            Check((float)calculate.Invoke(dynamics,new object[]{1f,75f}) == 0, "Slow hairpin brakes");
            float high = (float)calculate.Invoke(dynamics,new object[]{1f,120f});
            Check(high > 0 && high <= .25f, "Hard corner assistance out of bounds");
            var wheels = new[] { car.frontLeftWheel,car.frontRightWheel,car.rearLeftWheel,car.rearRightWheel };
            foreach (var wheel in wheels) wheel.motorTorque = 400;
            typeof(JrsVehicleController).GetField("rb",Private).SetValue(car,body);
            typeof(JrsVehicleController).GetField("tunedPhysics",Private).SetValue(car,true);
            typeof(JrsVehicleController).GetMethod("Update",Private).Invoke(car,null);
            Check(wheels.All(w=>w.motorTorque==400),"Render Update rewrote engine torque");
            typeof(RallyVehicleDynamics).GetField("automaticBrakeAmount",Private).SetValue(dynamics,.2f);
            typeof(RallyVehicleDynamics).GetMethod("UpdateBrakes",Private).Invoke(dynamics,null);
            Check(wheels.All(w=>w.motorTorque==400),"Automatic brake cut engine torque");
            foreach (bool split in new[]{false,true})
            {
                var canvas = new GameObject("Temporary HUD check",typeof(Canvas));
                if(split) new GameObject("Player Viewport",typeof(RectTransform)).transform.SetParent(canvas.transform,false);
                var hud=canvas.AddComponent<RallyRaceAssistHud>();hud.Configure(body,AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Settings/RallyHUD_Font.asset"));
                Check(!hud.WaterWarningVisible && hud.WarnedPuddle==null,"Water warning enabled");
                Check(!canvas.GetComponentsInChildren<TMP_Text>(true).Any(t=>(t.text??"").Contains("POZO")),"Water text remains");
                Check(canvas.GetComponentsInChildren<UnityEngine.UI.Image>(true).Length==1,"Water panel created");
                Object.DestroyImmediate(canvas);
            }
            File.AppendAllText(Report,$"PASS Circuit_0{index}: steering 55/96, normal-curve brake zero, hard-turn assist capped, engine torque preserved, interpolation, no water UI in 1P/2P\n");
        }
        string menu=File.ReadAllText("Assets/Scripts/RallyMenuController.cs");
        Check(!menu.Contains("Cada piloto elige") && !menu.Contains("TECLADO: J1") && !menu.Contains("localDeviceLabels"),"Marked menu instructions remain");
        string camera=File.ReadAllText("Assets/JS Vehicle Physics Controller/Script AMR/JrsFollowCamera.cs");
        Check(camera.Contains("void LateUpdate()") && !camera.Contains("void FixedUpdate()"),"Camera follows physics frames");
        File.AppendAllText(Report,"PASS: marked menu labels removed; follow camera updates render frames\nCOMPLETE: PASS\n");
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
    }
}
