import ast,json,re
from pathlib import Path
root=Path(__file__).parent
path=root/'detailed-deck.mjs'
source=path.read_text(encoding='utf-8')
start=source.index('export const notes=');end=source.index('\nfunction text(',start)
notes=json.loads(source[start+len('export const notes='):end].strip().rstrip(';'))
changes=json.loads((root/'plain-language-changes.json').read_text(encoding='utf-8'))
prepared=[]
for change in changes:
    n=change['slide'];pairs=[p for p in change.get('replacements',[]) if p[0]!=p[1]]
    for key,index in [('title',0),('subtitle',1)]:
        if key in change and notes[n-1][index]!=change[key]:
            pairs.append([notes[n-1][index],change[key]])
            notes[n-1][index]=change[key]
    prepared.append({'slide':n,'replacements':pairs})
    a=source.index(' if(n==='+str(n)+'){')
    b=source.index('\n if(n===',a) if n<22 else source.index('\n if(input.replace)',a)
    mapping=dict(pairs)
    def replace_literal(m):
        token=m.group(0)
        try:value=ast.literal_eval(token)
        except (ValueError,SyntaxError):return token
        return json.dumps(mapping[value],ensure_ascii=False) if value in mapping else token
    block=re.sub(r"'(?:\\.|[^'\\])*'",replace_literal,source[a:b])
    source=source[:a]+block+source[b:]
start=source.index('export const notes=');end=source.index('\nfunction text(',start)
source=source[:start]+'export const notes='+json.dumps(notes,ensure_ascii=False,indent=2)+';\n'+source[end:]
path.write_text(source,encoding='utf-8')
(root/'plain-language-prepared.json').write_text(json.dumps(prepared,ensure_ascii=False,indent=2),encoding='utf-8')
script_path=root/'presentation-script.json'
script=json.loads(script_path.read_text(encoding='utf-8'))
simple={
 '다섯 지역을 지나면서 점프와 회피로 길을 돌파하는 3D 횡스크롤 플랫포머입니다.':'3D 캐릭터로 다섯 지역을 지나며, 옆으로 이동하고 점프와 회피로 길을 돌파하는 게임입니다.',
 '기본 플레이와 씬 흐름':'기본 플레이와 화면 전환',
 '형태와 재질':'형태와 표면의 색감',
 '이 타임라인은':'이 일정은',
 '기믹의 조합':'여러 장치의 조합',
 'AI로 레벨을 만들 때도':'AI로 맵을 만들 때도',
 '테마별 리소스':'지역별 배경과 소품',
 '사용할 기믹':'사용할 장애물과 장치',
 '섬과 잔교 사이':'섬과 바다 위 다리 사이',
 '이런 동선을 만들려면':'이런 이동 경로를 만들려면',
 '플랫폼 아래로':'발판 아래로',
 '자동 검증기의 경고 기준':'자동 검사 도구의 경고 기준',
 '검증기 경고 기준':'자동 검사 기준',
 '오른쪽 속성 패널':'오른쪽 위치·크기 표시',
 '밟기 판정 흐름':'위에서 밟았는지 확인하는 흐름',
 '체크포인트와 저장은':'죽었을 때 다시 시작할 지점과 저장 기능은',
 '원하는 클리어 이펙트':'원하는 클리어 연출',
 '금빛 프레임과':'금빛 테두리와',
 'HUD에서는':'플레이 중 표시되는 정보에서는',
 '이런 가독성은 컴파일 성공만으로 판단할 수 없었습니다.':'코드에 오류가 없다고 해서 글자가 잘 읽히는 것은 아니었습니다.',
 '구획과 글자 배치':'정보를 담을 칸과 글자 배치',
 '시연 로그에서':'시연 실행 기록에서',
 '데이터 규칙':'파일에 기록할 항목과 형식',
 '게임 개발과 레벨 데이터':'게임 개발과 맵 배치',
 '이름과 축,':'이름과 방향,',
 '산출물의 규칙':'모델 파일의 크기와 이름',
 '안전한 소개부터 기믹의 조합까지':'안전하게 익힌 뒤 여러 장애물을 함께 통과하기'
}
for item in script:
    for key in ['cue','key','speech','transition']:
        for old,new in simple.items():item[key]=item[key].replace(old,new)
script[7]['speech']='맵툴은 발판과 적의 위치를 파일에 적고, 그 배치를 눈으로 보며 고칠 수 있는 도구입니다. 먼저 구간별로 어떤 경험을 줄지 정한 뒤, 위치와 크기를 숫자로 적은 배치 파일을 만듭니다.\n\n왼쪽 노란 블록을 고르면 오른쪽에 위치와 크기가 표시됩니다. AI와는 파일과 숫자를 기준으로 작업하고, 사람은 화면에서 길이 어떻게 이어지는지 확인할 수 있습니다. 같은 이름이 중복됐는지, 블록이 겹치는지, 적 아래에 발판이 있는지도 자동으로 검사합니다. 고친 파일은 게임에서 쓸 맵으로 만들어집니다.\n\n여기 보이는 초원 1-1은 이번 파일 검사에서 오류와 경고가 0건이었습니다. 실제로 쉽게 건널 수 있는지는 별도로 플레이하며 확인해야 합니다.'
script[7]['transition']='이처럼 정보를 나눠 정리하는 방식은 몬스터를 만들 때도 사용했습니다.'
script[8]['cue']='위의 행동 순서와 아래 전갈의 능력치 표를 연결해 설명합니다.'
script[8]['speech']='몬스터를 만들 때는 무엇을 할지와 얼마나 빠르고 강할지를 나눴습니다. 순찰하다 플레이어를 발견하면 쫓아가고, 놓치면 순찰로 돌아오는 행동 순서는 여러 몬스터가 함께 씁니다. 체력이나 속도, 공격 간격은 종류별 표에서 바꿉니다.\n\n아래 전갈도 같은 번호로 모델과 행동, 능력치를 연결했습니다. 이렇게 하면 속도를 바꾸고 싶을 때 해당 몬스터의 숫자를 찾아 고칠 수 있습니다. Claude와 개발 작업을 진행할 때도 무엇을 공통으로 쓰고 무엇을 종류별로 바꿀지 구분하는 기준이 됩니다.\n\n새로운 공격을 하게 만들려면 그 행동을 처리하는 프로그램도 추가해야 합니다.'
script[10]['speech']='보스전에서 만들고 싶었던 흐름은 관찰하고, 피하고, 기회를 잡는 것입니다. 보스가 공격을 예고하면 대응하고, 공격이 끝나 기절했을 때 머리의 약점을 밟아 피해를 줍니다.\n\n다섯 보스는 이 흐름을 함께 쓰지만 모습과 공격 조합을 다르게 했습니다. 체력이 줄어들면 다음 단계의 공격 조합으로 바뀝니다.\n\n개발할 때도 전투를 진행하는 기본 프로그램은 함께 쓰고, 보스마다 공격 종류와 수치를 바꾸는 방식으로 구성했습니다. 새 보스를 추가할 때마다 전투 전체를 다시 만드는 부담을 줄이기 위한 구조입니다. 일반 사망 때는 보스 체력을 유지해 다시 도전하도록 했습니다.'
script[12]['speech']='걷기와 달리기를 나눠 상황을 천천히 보거나 빠르게 이동할 수 있게 했습니다. 점프 버튼을 누르는 길이에 따라 높이도 조절할 수 있습니다.\n\n적을 위에서 밟았는지 확인하는 것도 중요했습니다. 빠르게 내려오다 적과 닿으면 캐릭터가 이미 적 안쪽에 들어와 있을 수 있습니다. 그래서 이동하기 전의 발 높이를 기억해 두고, 위에서 내려온 것인지 함께 확인하도록 했습니다.\n\n또 착지하기 직전, 0.1초 먼저 누른 점프를 잠깐 기억했다가 착지하면 실행합니다. 사용자가 누른 버튼을 놓치지 않도록 여유를 주는 기능입니다. 앞으로는 발판 끝을 벗어난 직후에도 잠깐 점프를 허용하는 기능을 검토하려고 합니다.'
script[13]['speech']='퍼펙트 회피는 타이밍을 맞춰 공격을 피했을 때 짧은 반격 기회를 주는 기능입니다. Shift로 빠르게 이동하며 회피 가능한 공격과 닿으면, 2초 동안 화면이 흑백으로 바뀌고 다른 대상이 멈춥니다. 그동안 플레이어는 움직일 수 있습니다.\n\n세 장은 실제 시연 영상의 연속 장면입니다. 가운데와 오른쪽을 비교하면 보스는 같은 자리에 있고 플레이어만 오른쪽으로 이동했습니다.\n\n게임 전체를 멈추면 플레이어도 멈추기 때문에, 플레이어의 이동과 동작은 따로 처리했습니다. 2초가 지나면 다른 대상도 다시 움직이게 합니다. 흑백 화면과 지나간 자리에 남는 잔상, 소리로 회피 성공을 알립니다.'
script[15]['speech']='모델의 모습은 세 단계를 거쳐 다듬었습니다. 처음에는 단순한 모양으로 지역을 구분했고, 다음에는 모서리를 부드럽게 하고 표면에 빛이 비치는 모습을 보완했습니다. 왼쪽의 수정 전후에서 차이를 볼 수 있습니다.\n\n이후에는 만화 같은 색감을 맞추고, 멀리 있는 모델은 더 간단한 형태로 보여주도록 제작 기준을 정했습니다. 가까이서는 세부 모양을 보여주고, 멀리서는 불필요하게 복잡한 모델을 쓰지 않으려는 것입니다.\n\nAI에 작업을 맡길 때도 크기, 모서리 모양, 색감처럼 눈으로 확인할 수 있는 조건으로 설명해야 했습니다. 아래의 면 수는 모델을 얼마나 잘게 나눠 만들었는지 나타냅니다. 과거 기록과 제작 목표이며, 현재 게임이 얼마나 빠르게 실행되는지 측정한 수치는 아닙니다.'
script[16]['speech']=script[16]['speech'].replace('Claude와 Blender MCP로 모델을 만들고','Claude가 Blender라는 3D 제작 프로그램을 다루도록 MCP로 연결해 모델을 만들고')
script[19]['speech']='UI의 시안과 글자 가독성, 화면 전환 연출에는 GPT Astra를 사용했습니다. 게임 개발과 맵 배치, 3D 모델 작업은 Claude로 진행했습니다.\n\n여기서 MCP는 AI와 작업 프로그램을 이어주는 연결 방식입니다. Coplay로 Unity 작업을 연결하고, Blender MCP로 3D 모델링 작업을 연결했습니다.\n\n배운 점은 요청 범위를 제가 먼저 분명하게 정해야 한다는 것입니다. 스테이지 1이라고 했을 때 1-1만 작업되는 일이 있어서, 이후에는 초원 1-1부터 1-4까지라고 전체 범위를 적었습니다. 참고 이미지는 실제 파일 경로로 전달했습니다.\n\n여러 결과물을 합칠 때는 이름과 방향, 파일에 적을 항목과 형식도 맞아야 합니다. 이런 조건을 정하고 결과가 의도에 맞는지 확인해 다음 수정을 결정하는 것이 제 역할이었습니다.'
script[20]['speech']='현재는 스테이지 파일과 몬스터의 구현 상태를 확인했고, 시연 실행 기록에서 다섯 보스의 공격과 기절, 체력이 줄어드는 장면을 확인했습니다. 이번 맵툴 촬영 과정에서도 Unity를 화면 없이 실행해 코드 오류를 확인하고, 초원 1-1 파일을 검사했습니다.\n\n다만 시연에는 자동 조작과 피해를 입지 않는 무적 설정을 사용했습니다. 실제 플레이어가 처음 보고도 공격을 피할 수 있는지, 끝까지 진행할 수 있는지는 별도로 확인해야 합니다. 기존 전체 검사 보고도 최신 완주 결과와 구분했습니다.\n\n다음에는 사람이 플레이하는 모습을 보며 용암 공격의 간격과 점프 입력의 여유를 조정하려고 합니다. 지역별 배경음악과, 어떤 기기에서 얼마나 부드럽게 실행할지도 기준을 정할 예정입니다.'
script_path.write_text(json.dumps(script,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'reviewedSlides':len(changes),'textReplacements':sum(len(c['replacements']) for c in prepared)},ensure_ascii=False))
