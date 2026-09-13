"""Add authored SierraDivision nested prefabs without rebuilding the user's rig.
Positions are bottom-centre, in the platform prefab's local metre coordinates.
Only the target prefab is mutated. A byte-for-byte backup precedes the write.
"""
from pathlib import Path
import re, math, json, shutil, hashlib, sys

ROOT = Path(__file__).resolve().parents[1]
TARGET = ROOT / 'Assets/Prefab/__OilRigGenerated.prefab'
SOURCE = ROOT / 'Assets/SierraDivision/Oil_Rig/Prefabs'
REPORT = ROOT / 'Tools/OilRigDetailReport'
original = TARGET.read_text(encoding='utf-8-sig')
if '--revise' in sys.argv:
    previous = json.loads((REPORT/'integrity.json').read_text())
    assert hashlib.sha256(TARGET.read_bytes()).hexdigest() == previous['sha256'], 'Prefab changed externally; stop'
    original = (REPORT/'__OilRigGenerated.before-details.prefab.backup').read_text(encoding='utf-8-sig')
assert '07_SierraDivision_DetailPass' not in original, 'Already applied; refusing duplicate dressing'
blocks = re.split(r'(?=^--- !u!)', original, flags=re.M)
used = set(re.findall(r'^--- !u!\d+ &(-?\d+)', original, re.M))
added, children, placements = [], {}, []

def ident(key):
    n = str(int(hashlib.sha256(key.encode()).hexdigest()[:15], 16))
    assert n not in used
    used.add(n)
    return n

def group(name, parent):
    go, tr = ident(name+'go'), ident(name+'tr')
    children.setdefault(parent, []).append(tr)
    children[tr] = []
    added.append((tr, f'''--- !u!1 &{go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {tr}}}
  m_Layer: 0
  m_Name: {name}
  m_TagString: Untagged
  m_IsActive: 1
--- !u!4 &{tr}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_Children: CHILDREN
  m_Father: {{fileID: {parent}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
'''))
    return tr

root = '3274338891570935283'
detail = group('07_SierraDivision_DetailPass', root)

def vec(text, key):
    m = re.search(r'  '+key+r': \{x: ([^,]+), y: ([^,]+), z: ([^}]+)\}', text)
    assert m, key
    return [float(v) for v in m.groups()]

def source_data(path):
    text = path.read_text(encoding='utf-8-sig')
    guid = re.search(r'guid: (\w+)', Path(str(path)+'.meta').read_text()).group(1)
    # Reuse Unity's already serialized root identifiers where available.
    template = next((b for b in blocks if b.startswith('--- !u!1001') and
                     f'm_SourcePrefab: {{fileID: 100100000, guid: {guid},' in b), None)
    if template:
        tid = re.search(r'target: \{fileID: (-?\d+),[^\n]+\n      propertyPath: m_LocalPosition.x', template).group(1)
        gid = re.search(r'target: \{fileID: (-?\d+),[^\n]+\n      propertyPath: m_Name', template)
        gid = gid.group(1) if gid else None
    else:
        tid = gid = None
    if tid is None or gid is None:
        instance = int(re.search(r'^--- !u!1001 &(-?\d+)', text, re.M).group(1))
        if tid is None:
            source_tid = int(re.search(r'target: \{fileID: (-?\d+),[^\n]+\n      propertyPath: m_LocalPosition.x', text).group(1))
            tid = str((source_tid ^ instance) & 0x7fffffffffffffff)
        gid = re.search(r'^--- !u!1 &(-?\d+) stripped', text, re.M).group(1)
    collider = next(b for b in re.split(r'(?=^--- !u!)', text, flags=re.M) if b.startswith('--- !u!65'))
    return guid, tid, gid, vec(collider,'m_Size'), vec(collider,'m_Center')

def place(parent, name, asset, position, yaw=0, height=None, maxwidth=None):
    path = SOURCE / asset
    assert path.is_file(), path
    guid, tid, gid, size, center = source_data(path)
    scale = height/size[1] if height else 1
    if maxwidth: scale = min(scale, maxwidth/max(size[0],size[2]))
    a = math.radians(yaw)
    c,s = math.cos(a),math.sin(a)
    offset = [scale*(c*center[0]+s*center[2]), scale*(center[1]-size[1]/2), scale*(-s*center[0]+c*center[2])]
    pos = [position[i]-offset[i] for i in range(3)]
    iid, tr = ident(name+'instance'), ident(name+'transform')
    values = [(tid,'m_LocalPosition.'+k,pos[i]) for i,k in enumerate('xyz')]
    values += [(tid,'m_LocalRotation.'+k,v) for k,v in zip('xyzw',[0,math.sin(a/2),0,math.cos(a/2)])]
    values += [(tid,'m_LocalScale.'+k,scale) for k in 'xyz']
    values += [(gid,'m_Name',name)]
    mods = ''.join(f'    - target: {{fileID: {target}, guid: {guid}, type: 3}}\n      propertyPath: {key}\n      value: {value}\n      objectReference: {{fileID: 0}}\n' for target,key,value in values)
    added.append((None, f'''--- !u!1001 &{iid}
PrefabInstance:
  m_ObjectHideFlags: 0
  serializedVersion: 2
  m_Modification:
    serializedVersion: 3
    m_TransformParent: {{fileID: {parent}}}
    m_Modifications:
{mods}    m_RemovedComponents: []
    m_RemovedGameObjects: []
    m_AddedGameObjects: []
    m_AddedComponents: []
  m_SourcePrefab: {{fileID: 100100000, guid: {guid}, type: 3}}
--- !u!4 &{tr} stripped
Transform:
  m_CorrespondingSourceObject: {{fileID: {tid}, guid: {guid}, type: 3}}
  m_PrefabInstance: {{fileID: {iid}}}
  m_PrefabAsset: {{fileID: 0}}
'''))
    children[parent].append(tr)
    footprint = [scale*(abs(c)*size[0]+abs(s)*size[2]), scale*(abs(s)*size[0]+abs(c)*size[2])]
    placements.append(dict(name=name,asset=asset,position=position,footprint=footprint,height=size[1]*scale))

cab='Electrical_Structures/ElectricBox_Module_1.prefab'
cab3='Electrical_Structures/ElectricBox_Module_3.prefab'
console='Control_Console/Control_Console_B.prefab'
crate='Crate_Metal/BarrelCrate_Square_02_.prefab'
barrel='Barrel_Metal/Barrel_Oil_Metal_02_02_.prefab'

prep=group('01_Preparation_Stores',detail)
for i,x in enumerate([-16.5,-15,-13.5]):
    place(prep,f'Prep_Storage_{i}',crate,[x,6,-9],height=.85,maxwidth=1.15)
    place(prep,f'Prep_Upper_Storage_{i}',crate,[x,6.85,-9],height=.55,maxwidth=.85)
place(prep,'Prep_Service_Cabinet',cab,[-17.2,6,-3.3],90,height=1.8,maxwidth=1.1)
place(prep,'Prep_Barrel_Trolley','Barrel_Trolley/Barrel_Trolley_Assembled_01_.prefab',[-11.3,6,-8.7],height=1.2,maxwidth=.8)

electrical=group('02_Electrical_Switchgear',detail)
for i,z in enumerate([-9,-7.6,-6.2]):
    place(electrical,f'Switchgear_{i}',cab3,[-9.3,6,z],90,height=1.9,maxwidth=1)
place(electrical,'Spares_Crate',crate,[-7,6,-9.1],height=.7,maxwidth=1.1)

for title,floor in [('03_Monitoring_Room',6),('04_Control_Room',10)]:
    room=group(title,detail)
    for i,x in enumerate([-16.8,-15.4,-14,-12.6,-11.2]):
        place(room,f'{title}_Window_Console_{i}',console,[x,floor,9],180,height=1.05,maxwidth=1)
    for i,z in enumerate([3.1,4.5,5.9]):
        place(room,f'{title}_Data_Cabinet_{i}',cab,[-17.3,floor,z],90,height=1.75,maxwidth=.95)

analysis=group('05_Analysis_Service_Interior',detail)
for i,x in enumerate([-8.6,-7.2,-5.8,-4.4]):
    place(analysis,f'Analysis_Instrument_{i}',console,[x,10,9],180,height=1.05,maxwidth=1)
for i,z in enumerate([3.2,4.7,6.2]):
    place(analysis,f'Analysis_Sample_Store_{i}',crate,[-2.9,10,z],height=.8,maxwidth=1.1)

deck=group('06_Deck_Logistics_Bays',detail)
for i,x in enumerate([-16.5,-14.2,-11.9]):
    place(deck,f'Roof_Stores_{i}',crate,[x,10,-8.8],height=1.05,maxwidth=1.7)
for i,x in enumerate([1,3.4,5.8]):
    place(deck,f'South_Cargo_{i}','Crate_Metal/BarrelCrate_Rectangular_01_Tarp.prefab',[x,6,-9],90,height=1.0,maxwidth=1.7)
for i,(x,z) in enumerate([(17.8,-8.8),(17.8,-7.5),(17.8,-6.2),(16.6,-8.8)]):
    place(deck,f'Fuel_Service_Barrel_{i}',barrel,[x,6,z],height=.95,maxwidth=.65)
place(deck,'Fuel_Handling_Trolley','Barrel_Trolley/Barrel_Trolley_Assembled_01_.prefab',[16.4,6,-6.2],90,height=1.2,maxwidth=.9)

plant=group('07_Roof_HVAC_And_Switchboards',detail)
for i,x in enumerate([-8,-5]):
    place(plant,f'Roof_HVAC_{i}','Electrical_Structures/ACunit_Small_Module_3_.prefab',[x,14,8],height=1.3,maxwidth=2)
for i,x in enumerate([-9,-7.2]):
    place(plant,f'Exterior_Switchboard_{i}',cab,[x,6,-10.65],180,height=1.65,maxwidth=1)

# Supported process service pipe bank occupies the north service edge, not the central walkway.
pipes=group('08_North_Process_Pipe_Bank',detail)
for row,z in enumerate([10.8,11.3]):
    for i,x in enumerate([1.8,5.8,9.8,13.8]):
        place(pipes,f'Process_Line_{row}_{i}','Pipes_Small/Pipe_Sml_Strt_4m_01_.prefab',[x,6.55,z],0,height=.28721023*4/2.8018937)
    for i,x in enumerate([1,7,13]):
        place(pipes,f'Isolation_Valve_{row}_{i}','Valves_Small/Valve_Small_01_.prefab',[x,6.5,z],90,height=.4,maxwidth=.5)
        place(pipes,f'Pipe_Support_{row}_{i}','Pipes_Brackets/Pipe_Bracket_Small_LBeam_Uniform.prefab' if (SOURCE/'Pipes_Brackets/Pipe_Bracket_Small_LBeam_Uniform.prefab').exists() else 'Pipes_Brackets/Pipe_Bracket_Medium_LBeam_Uniform.prefab',[x,6,z],0,height=.55,maxwidth=.6)

outfit=group('09_External_Service_Detail',detail)
for i,(x,z) in enumerate([(-18.7,-8),(-18.7,-4),(-18.7,4),(-18.7,8)]):
    place(outfit,f'External_Junction_{i}',cab,[x,7.7,z],90,height=.55,maxwidth=.45)
for i,(x,z) in enumerate([(-16.5,-8.7),(-12,-8.7),(-16.5,8.7),(-5,8.7),(17,-9),(17,9)]):
    place(outfit,f'Deck_Worklight_{i}','Flood_Lights/Floodlights_01_Empty.prefab',[x,10 if x<0 and z<0 else (14 if x<0 else 6),z],180,height=1.4,maxwidth=.8)

# Fail before touching the prefab if any added floor-standing interior object exceeds its room.
for p in placements:
    x,y,z=p['position']; w,d=p['footprint']
    if p['name'].startswith(('Prep_','03_','04_','Analysis_','Switchgear','Spares_')):
        if z<0: limits=(-18,-6,-10,-2)
        elif p['name'].startswith('Analysis_'): limits=(-10,-2,2,10)
        else: limits=(-18,-10,2,10)
        assert x-w/2>=limits[0] and x+w/2<=limits[1] and z-d/2>=limits[2] and z+d/2<=limits[3], p

out=[]
for b in blocks:
    if b.startswith(f'--- !u!4 &{root}\n'):
        b=b.replace('  m_Children:\n','  m_Children:\n'+''.join(f'  - {{fileID: {c}}}\n' for c in children[root]),1)
    out.append(b)
for tr,b in added:
    if tr: b=b.replace('CHILDREN','\n'+''.join(f'  - {{fileID: {c}}}\n' for c in children[tr]).rstrip())
    out.append(b)
result=''.join(out)
ids=re.findall(r'^--- !u!\d+ &(-?\d+)',result,re.M)
assert len(ids)==len(set(ids)), 'Duplicate Unity file IDs'
REPORT.mkdir(parents=True,exist_ok=True)
backup=REPORT/'__OilRigGenerated.before-details.prefab.backup'
if '--revise' not in sys.argv:
    assert not backup.exists(), 'Preserve existing backup'
    shutil.copy2(TARGET,backup)
TARGET.write_text(result,encoding='utf-8')
(REPORT/'integrity.json').write_text(json.dumps({'sha256':hashlib.sha256(TARGET.read_bytes()).hexdigest()}))
(REPORT/'placements.json').write_text(json.dumps(placements,indent=2),encoding='utf-8')
print(f'Saved {TARGET}: {len(placements)} nested SierraDivision prefabs; all existing objects preserved.')
