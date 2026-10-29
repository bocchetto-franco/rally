"""Compare protected serialized physics and race objects with this task's backups."""
import re
from pathlib import Path
root=Path(__file__).resolve().parents[1]
backup=root/'Logs/Gameplay_20261028/Before'
protected_fields=('vehicleMass','vehicleAngularDamping','forwardExtremumSlip','forwardExtremumValue','forwardAsymptoteSlip','forwardAsymptoteValue','forwardStiffness','rearForwardExtremumValue','rearForwardAsymptoteValue','frontSideExtremumSlip','frontSideExtremumValue','frontSideAsymptoteSlip','frontSideAsymptoteValue','frontSideStiffness','rearSideExtremumSlip','rearSideExtremumValue','rearSideAsymptoteSlip','rearSideAsymptoteValue','rearSideStiffness','minimumDownforceSpeedKph','downforceCoefficient','maximumDownforce','suspensionDistance','springForce','springDamper','springTargetPosition','centerOfMassHeight','centerOfMassLongitudinalOffset')
def docs(path):
    text=path.read_text(encoding='utf-8-sig')
    matches=list(re.finditer(r'^--- !u!(\d+) &(-?\d+)[^\n]*\n',text,re.M))
    return {m[2]:(int(m[1]),text[m.end():matches[i+1].start() if i+1<len(matches) else len(text)].strip()) for i,m in enumerate(matches)}
report=[]
for name in ('Circuit_01','Circuit_02','Circuit_03'):
    before=docs(backup/f'Assets/Scenes/{name}.unity');after=docs(root/f'Assets/Scenes/{name}.unity')
    counts={54:0,146:0}
    for key,(kind,text) in before.items():
        if kind in counts:
            assert key in after and after[key]==(kind,text),f'{name}: rigidbody/wheel physics changed: {key}'
            counts[kind]+=1
        if 'guid: 8bde151bd5691d44ea6269aba41903aa' in text:
            now=after[key][1]
            for field in protected_fields:
                pattern=rf'^  {re.escape(field)}: (.*)$'
                assert re.findall(pattern,text,re.M)==re.findall(pattern,now,re.M),f'{name}: protected tuning {field} changed'
        if kind==114 and ('CheckpointTrigger' in text or 'CheckpointManager' in text):
            assert after[key][1]==text,f'{name}: checkpoint logic/configuration changed: {key}'
        if kind==1 and re.search(r'^  m_Name: (Waypoint_\d+|AI_Waypoints|Checkpoint.*)$',text,re.M):
            transforms=re.findall(r'^  - component: \{fileID: (-?\d+)\}',text,re.M)
            for transform in transforms:
                if before.get(transform,(None,))[0]==4:assert before[transform]==after[transform],f'{name}: route transform changed: {transform}'
        if kind==114 and 'guid: 40e9e2e80697d254cae23400cd1aa8c4' in text:
            assert text==after[key][1],f'{name}: puddle gameplay settings changed'
        if kind==114 and 'guid: 8a752812d60d4b12ad1babc1590e0011' in text:
            now=after[key][1]
            for field in ('durationSeconds','accelerationMps2','maximumSpeedKph','botSpeedBonusKph','rearmSeconds'):
                p=rf'^  {field}: (.*)$';assert re.findall(p,text,re.M)==re.findall(p,now,re.M),f'{name}: boost effect changed: {field}'
    report.append(f'PASS {name}: {counts[54]} Rigidbody, {counts[146]} WheelCollider unchanged; base dynamics/downforce, route transforms, puddles and boost effect preserved')
report.append('COMPLETE: PASS')
(root/'Logs/Gameplay_20261028/preservation.txt').write_text('\n'.join(report)+'\n',encoding='utf-8')
print('\n'.join(report))
