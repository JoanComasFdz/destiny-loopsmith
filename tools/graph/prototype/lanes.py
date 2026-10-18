import yaml,glob,json,sys,re
graph=json.load(open(sys.argv[1])); out=sys.argv[2]
scope=set(next(s for s in graph['scopes'] if s['id']=='withDependents')['nodes'])
gl=yaml.safe_load(open('rules/glossary.yaml'))
G={}
for sec in ('statuses','pickups','summons'):
    for s in gl.get(sec) or []: G[s['id']]=s
els={}
for f in sorted(glob.glob('rules/**/*.yaml',recursive=True)):
    if f.endswith(('glossary.yaml','manifest.yaml')): continue
    for e in yaml.safe_load(open(f))['elements']:
        if 'inert' not in e: els[e['id']]=e
kebab=lambda s: re.sub(r'(?<!^)(?=[A-Z])','-',s).lower()
def owner(i): return ('rule:'+i) if (els[i]['kind']=='keyword' and i in G) else i
def st(t):
    if t in els and els[t]['kind']!='keyword': return 'status:'+t
    return t
def nm(t): return G.get(t,{}).get('name',t)
TRIG={'grenade':('trigger:grenade','Grenade'),'melee':('trigger:melee','Melee'),'melee:press':('trigger:melee','Direct melee'),'melee:slide':('trigger:sliding-melee','Sliding melee'),
      'super':('trigger:super','Super'),'classAbility':('trigger:dodge','Dodge')}
ABIL=['grenade','melee','melee:slide','super']
def via(v):
    if v in TRIG: return [TRIG[v]]
    if v=='ability': return [TRIG[a] for a in ABIL]
    if v=='any' or v.startswith('weapon:this'): return [(None,'Any' if v=='any' else 'This weapon')]
    if v=='weapon': return [('weapon:weapon','Weapon')]
    if v.startswith('weapon:'): t=v.split(':')[1]; return [('weapon:'+t+'-weapon',t.capitalize()+' weapon')]
    if v.startswith('slot:'): t=v.split(':')[1]; return [(None,t.capitalize()+' weapon')]
    if v.startswith('keyword:'): t=v.split(':')[1]; return [('strike:'+t,nm(t)+' strike')]
    if v.startswith('summon:'): t=v.split(':')[1]; return [(t,nm(t))]
    if v.startswith('type:'): t=v.split(':')[1]; return [(None,t.capitalize()+' damage')]
    return [(None,v)]
EN={'grenade':'grenade','melee':'melee','classAbility':'class-ability','super':'super','leastCharged':'least-charged'}
def outcome(o):
    k,v=list(o.items())[0]
    if k in('applyBuff','debuffTarget'):
        s=v if isinstance(v,str) else v['status']; x=[]
        if isinstance(v,dict):
            if v.get('stacks',1)!=1: x.append(f"×{v['stacks']}")
            if v.get('duration'): x.append(v['duration'])
        return [('gives',st(s),' '.join(x))]
    if k in('spawn','summon'): return [('gives',st(v),'')]
    if k=='strikeTarget': return [('gives','strike:'+v['via'] if v['via'] in G or v['via'] in els else 'effect:extra-strike','')]
    if k=='grantEnergy': return [('gives','effect:energy-'+EN[v['to']],str(v.get('amount','')))]
    if k=='convertStacksToEnergy': return [('ends',st(v['consumed']),'spends all'),('gives','effect:energy-'+EN[v['to']],'by stacks')]
    if k=='spendStacks': return [('ends',st(v['status']),f"spends {v['count']}")]
    if k=='removeBuff': return [('ends',st(v),'')]
    if k=='restoreHealth': return [('gives','effect:health',str(v.get('amount',v.get('hp','')))+(' + allies' if v.get('allies') else ''))]
    if k=='modifyDamage': return [('gives','effect:damage-bonus',f"{v['change']} vs {v['against']}")]
    if k=='reloadWeapon': return [('gives','effect:reload','')]
    if k=='collect': return [('collects',st(v),'')]
    return [('gives',None,k)]
def passive(p):
    k,v=list(p['effect'].items())[0]
    if k in('resistDamage','resistCombatantDamage'): return [('gives','effect:damage-resistance',v if isinstance(v,str) else f"{v['amount']} vs {v['against']}")]
    if k=='modifyDamage': return [('gives','effect:damage-bonus',f"{v['change']} vs {v['against']}")]
    if k=='modifyWeaponStats': return [('gives','effect:weapon-stats','')]
    if k=='modifyStat': return [('gives','effect:stat-'+v['stat'],v['change'])]
    if k=='extraCharges': return [('gives','effect:extra-charge-'+v['ability'],f"+{v['extra']}")]
    if k=='extraStacks': return [('boosts',st(v['status']),f"+{v['extra']} stack")]
    if k=='raiseMaxStacks': return [('boosts',st(v['status']),f"max {v['max']}")]
    return [('gives',None,k)]
def conds(when):
    c=[]
    for w in when or []:
        if 'has' in w: c.append((st(w['has']),"on you"))
        if 'atMax' in w: c.append((st(w['atMax']),"at max stacks"))
        if 'holds' in w: c.append((st(w['holds']['status']),f"{w['holds']['atLeast']}+ stacks"))
        if 'lacks' in w: c.append((None,f"no {nm(w['lacks'])}"))
        if 'during' in w: c.append((None,f"during your {w['during']}"))
        if 'belowMax' in w: c.append((None,f"{nm(w['belowMax'])} below max"))
    return c
lanes=[]
for i,e in els.items():
    o=owner(i)
    if o not in scope: continue
    for n,r in enumerate(e.get('rules') or []):
        on=r.get('on') or r.get(True); k,v=list(on.items())[0]
        trig=[]; tc=[]; word=''
        if k=='abilityCast':
            trig=[('trigger:air-dodge','Air dodge')] if isinstance(v,dict) else [TRIG[v]]; word='cast'
            if trig[0][0]=='trigger:dodge' and False: pass
        elif k in('kill','damage'):
            trig=via(v['via']); word='kill' if k=='kill' else ('sustained hit' if v.get('sustained') else 'hit')
            if 'atLeast' in v: word=f"{v['atLeast']}+ {'kills' if k=='kill' else 'hits'}"+(f" within {v['within']}" if v.get('within') else '')
            if 'fewerThan' in v: word=f"fewer than {v['fewerThan']} {'kills' if k=='kill' else 'hits'}"
            if v.get('tier'): word+=f" ({v['tier']})"
            for t in v.get('targetHas') or []: tc.append((st(t),"on the target"))
        elif k=='pickUp': trig=[(st(v),'Pick up '+nm(v))]; word=''
        elif k=='finisher': trig=[('trigger:finisher','Finisher')]
        elif k=='buffGained': trig=[(st(v),nm(v)+' gained')]
        elif k=='stacksMaxed': trig=[(st(v),nm(v)+' at max')]
        elif k=='energyExpended': trig=[(TRIG[v][0],TRIG[v][1]+' (last charge)')]
        elif k=='shieldBroken': trig=[(None,'Shield broken')]
        elif k=='criticalHealth': trig=[('trigger:critical-health','Critical health')]
        if k=='abilityCast' and not isinstance(v,dict): word='cast' if v!='classAbility' else ''
        res=[x for oo in r['then'] for x in outcome(oo)]
        res=[x for x in res if not (x[0]=='ends' and x[1] in (i,'status:'+i))] + [x for x in res if x[0]=='ends' and x[1] in (i,'status:'+i)]
        lanes.append(dict(id=f"{o}#{n}",element=o,triggers=[{'node':a,'text':(b+(' '+word if word else '')).strip()} for a,b in trig],
            conditions=[{'node':a,'text':b} for a,b in tc+conds(r.get('when'))],
            results=[{'kind':a,'node':b,'text':c} for a,b,c in res],
            chance=bool(r.get('chance')) or ('fewerThan' in v if isinstance(v,dict) else False),reason=r.get('reason','')))
    for n,p in enumerate(e.get('passives') or []):
        lanes.append(dict(id=f"{o}#p{n}",element=o,triggers=[{'node':None,'text':'Always'}],conditions=[{'node':a,'text':b} for a,b in conds(p.get('when'))],
            results=[{'kind':a,'node':b,'text':c} for a,b,c in passive(p)],chance=False,reason=p.get('reason','')))
# One lane per trigger: a rule whose trigger names several (any ability) becomes one lane for each, so each lane is one
# claim a scenario can check. The rules file keeps one rule.
split=[]
for l in lanes:
    if len(l['triggers'])<=1: split.append(l); continue
    for k,tr in enumerate(l['triggers']): split.append(dict(l,id=f"{l['id']}.{k}",triggers=[tr]))
lanes=split
json.dump(lanes,open(out,'w'),indent=1)
print(len(lanes),'lanes')
for l in lanes:
    if l['element'] in('lethal-current','gamblers-dodge','spark-of-brilliance'): print(l['element'],[t['text'] for t in l['triggers']],[c['text'] for c in l['conditions']],[(r['kind'],r['node'],r['text']) for r in l['results']])
