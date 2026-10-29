using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Render-only vibration: restores the exact follow pose after each camera.</summary>
[DefaultExecutionOrder(1100)]
[RequireComponent(typeof(Camera), typeof(JrsFollowCamera))]
public sealed class RallyCameraRoadShake : MonoBehaviour
{
    [SerializeField, Range(0f,.4f)] float intensity = .025f;
    [SerializeField, Range(0f,2f)] float rotationDegrees = .12f;
    [SerializeField, Min(1f)] float fullIntensitySpeedKph = 110f;
    JrsFollowCamera follow;
    Camera view;
    Rigidbody body;
    JrsVehicleController car;
    RallyOffTrackSpeedPenalty road;
    RallyWaterSurface[] puddles;
    Transform owner;
    WheelCollider[] wheels;
    float strength;
    Vector3 offset, basePosition;
    Quaternion rotation, baseRotation;
    bool applied;
    public float Intensity => intensity;
    public float CurrentStrength => strength;

    void OnEnable()
    {
        follow=GetComponent<JrsFollowCamera>();view=GetComponent<Camera>();
        RenderPipelineManager.beginCameraRendering+=BeforeRender;
        RenderPipelineManager.endCameraRendering+=AfterRender;
    }
    void Bind()
    {
        owner=follow.target;body=owner!=null?owner.GetComponentInParent<Rigidbody>():null;
        car=body!=null?body.GetComponent<JrsVehicleController>():null;
        road=body!=null?body.GetComponent<RallyOffTrackSpeedPenalty>():null;
        wheels=car==null?new WheelCollider[0]:new[]{car.frontLeftWheel,car.frontRightWheel,car.rearLeftWheel,car.rearRightWheel};
        puddles=RallyWaterSurface.Read(gameObject.scene);strength=0;
    }
    void LateUpdate()
    {
        Restore();
        if(follow.target!=owner || wheels==null)Bind();
        float target=0;
        if(body!=null && car!=null && car.enabled && Time.timeScale>0 && !RallyRaceLoading.IsPreparing && !body.isKinematic && Vector3.Dot(body.transform.up,Vector3.up)>.5f)
        {
            int grounded=0,onRoad=0;
            foreach(var wheel in wheels)if(wheel!=null && wheel.GetGroundHit(out WheelHit hit)) {grounded++;if(road!=null && road.IsDrivingSurface(hit.collider))onRoad++;}
            if(grounded>=2)
            {
                float speed=Vector3.ProjectOnPlane(body.linearVelocity,Vector3.up).magnitude*3.6f;
                // Suspension noise alone must not shake the camera on ordinary road.
                float roughness=0f;
                if(onRoad<grounded)roughness=Mathf.Max(roughness,.55f);
                foreach(var puddle in puddles)if(puddle.Contains(body)){roughness=Mathf.Max(roughness,.9f);break;}
                target=roughness*Mathf.InverseLerp(8,fullIntensitySpeedKph,speed);
            }
        }
        strength=Mathf.MoveTowards(strength,target,Time.unscaledDeltaTime*4f);
        if(Time.timeScale<=0 || RallyRaceLoading.IsPreparing)strength=0;
        float t=Time.time*16f, phase=(float)(EntityId.ToULong(GetEntityId())%997UL);
        float x=Mathf.PerlinNoise(t,phase)*2-1,y=Mathf.PerlinNoise(phase,t)*2-1;
        offset=new Vector3(x,y*.65f,0)*intensity*strength;
        rotation=Quaternion.Euler(y*rotationDegrees*strength,0,x*rotationDegrees*strength);
    }
    void BeforeRender(ScriptableRenderContext context,Camera camera)
    {
        if(camera!=view || applied || strength<=0 || !follow.enabled)return;
        basePosition=transform.position;baseRotation=transform.rotation;applied=true;
        transform.position+=transform.rotation*offset;transform.rotation*=rotation;
    }
    void AfterRender(ScriptableRenderContext context,Camera camera){if(camera==view)Restore();}
    void Restore(){if(!applied)return;transform.SetPositionAndRotation(basePosition,baseRotation);applied=false;}
    void OnDisable(){RenderPipelineManager.beginCameraRendering-=BeforeRender;RenderPipelineManager.endCameraRendering-=AfterRender;Restore();}
}
