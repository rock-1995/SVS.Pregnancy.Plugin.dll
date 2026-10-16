import sys, json
from pathlib import Path
import UnityPy
from UnityPy.helpers.MeshHelper import MeshHandler

source=sys.argv[1]
env=UnityPy.load(source)
meshes=[]
seen=set()
for obj in env.objects:
    if obj.type.name!='SkinnedMeshRenderer': continue
    smr=obj.read()
    if not smr.m_Mesh.path_id or smr.m_Mesh.path_id in seen: continue
    seen.add(smr.m_Mesh.path_id)
    bones=[p.read() if p.path_id else None for p in smr.m_Bones]
    names=[b.m_GameObject.read().m_Name if b else '' for b in bones]
    if not any(n.startswith('cf_j_sk_') for n in names): continue
    mesh=smr.m_Mesh.read()
    handler=MeshHandler(mesh); handler.process()
    lookup={p.path_id:i for i,p in enumerate(smr.m_Bones) if p.path_id}
    parents=[]
    for b in bones:
        parent=-1
        p=b.m_Father if b else None
        while p and p.path_id:
            if p.path_id in lookup: parent=lookup[p.path_id];break
            p=p.read().m_Father
        parents.append(parent)
    out=dict(name=mesh.m_Name,names=names,parents=parents,vertices=handler.m_Vertices,
        triangles=[i for sub in handler.get_triangles() for tri in sub for i in tri],
        bindposes=[[getattr(b,f'e{r}{c}') for c in range(4) for r in range(4)] for b in mesh.m_BindPose],
        weights=handler.m_BoneWeights,indices=handler.m_BoneIndices)
    meshes.append(out)
    print(mesh.m_Name,len(handler.m_Vertices),'vertices',len(names),'bones',flush=True)
Path(sys.argv[2]).write_text(json.dumps(dict(source=source,meshes=meshes)),encoding='utf8')

