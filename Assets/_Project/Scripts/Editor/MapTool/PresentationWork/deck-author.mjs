const ROOT='C:/Users/PC/UnityStudy/MyGame/Assets/_Project/';
const PRE=ROOT+'Scripts/Editor/MapTool/UiUpgradeWork/Verification/';
const C={bg:'#10151C',white:'#F5EAD2',gold:'#DDB46A',muted:'#B7BEC8',line:'#38424E',blue:'#8DC9E5'};
function text(s,value,x,y,w,h,size=28,color=C.white){
 const t=s.shapes.add({geometry:'textbox',position:{left:x,top:y,width:w,height:h},fill:'none',line:{fill:'none',width:0}});
 t.text=value;t.text.style={typeface:'Noto Sans KR',fontSize:size,color,bold:false,insets:0,verticalAlignment:'middle',autoFit:'none'};return t;
}
function rect(s,x,y,w,h,fill,line='none'){return s.shapes.add({geometry:'rect',position:{left:x,top:y,width:w,height:h},fill,line:{fill:line,width:line==='none'?0:1}});}
function line(s,x,y,w){rect(s,x,y,w,2,C.line);}
function base(p,n,title){const s=p.slides.add({width:1280,height:720});s.background.fill=C.bg;text(s,title,64,44,1130,72,42);line(s,64,137,1152);text(s,String(n).padStart(2,'0'),1176,662,40,26,16,C.muted);return s;}
function image(s,path,x,y,w,h){s.images.add({path,position:{left:x,top:y,width:w,height:h},fit:'contain'});}
export const notes=[
{title:'LEAF & LEAP',seconds:20,text:'LEAF & LEAP는 점프와 회피를 연결하는 2.5D 액션 플랫포머입니다. 공격을 회피하는 순간 세계가 멈추고 플레이어만 움직이는 기회를 얻습니다. 오늘은 이 플레이 경험과, 다섯 지역의 맵을 만드는 제작 구조를 소개하겠습니다.',source:'Resources/TitleArt/Meadow.png. 타이틀 원화이며 플레이 캡처가 아닙니다.'},
{title:'게임의 흐름과 현재 규모',seconds:30,text:'월드맵에서 스테이지를 선택하고, 점프와 전투로 구간을 통과한 뒤 기록과 수집 결과를 확인하는 구조입니다. 현재 다섯 지역에 일반 스테이지 스무 개와 보스 스테이지 다섯 개가 등록돼 있습니다. 몬스터는 테이블 기준 서른 개가 구현 상태이며 프리팹 파일도 있습니다. 이 숫자는 등록 규모이고, 모든 맵의 밸런스 검증을 끝냈다는 의미는 아닙니다.',source:'Data/StageDatabase.asset, Data/StageLayouts/W*.json, Data/Tables/MonsterTable.csv, ProjectSettings/EditorBuildSettings.asset. 그림은 타이틀 UI 정적 렌더.'},
{title:'회피 성공으로 얻는 2초',seconds:55,text:'대표 기능은 회피 성공에 따른 시간 정지입니다. 회피 동작 중 적이나 보스의 회피 가능한 공격과 접촉하면, 피해를 피하면서 약 이 초의 시간을 얻습니다. 이때 세계는 멈추지만 플레이어는 계속 움직입니다. 화면을 흑백으로 전환하고 시계 소리와 음악 감쇠로 상태를 전달합니다. 구현에서는 월드 시간을 멈춘 뒤 플레이어 물리만 별도로 계산하고, 종료 시 원래 속도와 상태를 복구합니다. 이 기회를 다음 발판으로 이동하거나 적을 넘어가는 데 사용할 수 있습니다.',cue:'선택 시연: 적 공격에 맞춰 회피하는 장면 10초. 준비된 영상이 없으면 슬라이드 도식으로 설명합니다. 이번 자료에 녹화 영상은 포함하지 않았습니다.',source:'Scripts/InGame/FX/TimeStop.cs, PlayerMotor.ManualStep, InGameController.OnPlayerHit 및 PerfectDodge. 06_InGame 씬 perfectDodgeTimeStop=2. 도식은 동작 설명용이며 실제 캡처가 아닙니다.'},
{title:'보스전의 예고와 반격',seconds:40,text:'보스전은 예고를 보고 공격을 피한 다음, 약점이 열리는 순간을 노리도록 구성했습니다. 돌진, 충격파, 낙하 공격을 데이터로 조합하며 용암 보스에는 순차 분출도 있습니다. 공격 뒤 기절한 보스를 밟아 피해를 주고, 남은 체력에 따라 다음 패턴으로 넘어갑니다. 공통 로직에 보스별 데이터와 외형을 연결하는 방식이므로 콘텐츠를 추가할 때 공통 규칙을 유지하기 쉽습니다.',source:'Scripts/InGame/Boss/BossController.cs, Scripts/Data/BossData.cs, Data/Bosses/Boss_9001~9005.asset. 흐름은 구현 구조를 단순화한 설명입니다.'},
{title:'다섯 지역의 플레이 구성',seconds:35,text:'지역은 배경 색뿐 아니라 발판과 적의 조합으로 구분합니다. 초원에서는 기본 이동과 밟기 공격을 익히고, 사막에서는 트랩과 스위치를 조합합니다. 설산에는 가속과 제동을 바꾸는 얼음 바닥이 있습니다. 산호 해안에서는 바위섬과 이동 발판을 연결하고, 용암 요새에서는 이동 타이밍과 강한 보스 패턴을 강조합니다. 지역마다 일반 네 개와 보스 한 개의 구성을 갖추고 있으며, 난이도의 연결성은 계속 점검할 부분입니다.',source:'Data/StageLayouts/W1~W5 JSON. W3 얼음 블록 9개, W2 가시 트랩 12개, W4 이동 발판 6개, W5 이동 발판 11개 확인. 배치 수를 재미나 난이도의 검증 결과로 해석하지 않습니다.'},
{title:'맵 제작을 위한 편집·검증 도구',seconds:55,text:'개발 측면의 핵심은 전용 맵툴입니다. 발판과 적, 체크포인트, 수집품을 배치한 뒤 레이아웃을 JSON으로 저장하고, 같은 데이터에서 씬을 생성합니다. 그리드 맞춤, 표면 맞춤, 복제와 실행 취소도 지원합니다. 검증기는 중복 ID, 잘못된 참조, 블록 겹침 등을 확인하고 거리와 높이 차이로 기본적인 도달 가능성 경고를 제공합니다. 자동화 진입점이 있어 AI나 외부 도구가 만든 데이터에도 같은 검증 절차를 적용할 수 있습니다. 다만 경고가 없다고 실제 클리어까지 보장하는 것은 아닙니다.',cue:'선택 시연: 발판 배치 수정, 겹침 경고 확인, 재검증 후 씬 생성. 실시간 시연이 어려우면 이 단계별 도식으로 설명합니다.',source:'Scripts/Editor/MapTool/StageMapAutomation.cs, StageLayoutJson.cs, StageMapController.cs, StageMapSceneOverlay.cs, Scripts/Editor/StageLayoutValidator.cs. 도식은 파이프라인 설명용.'},
{title:'결과를 읽을 수 있게 정리한 UI',seconds:30,text:'최근에는 화면마다 달랐던 글자 크기와 정렬을 공통 규칙으로 정리했습니다. Noto Sans와 검정·금빛 미감을 유지하고, 클리어 결과를 시간, 코인, 특별 수집품으로 나눴습니다. 시간 칸에는 신기록을 함께 표시합니다. 이 화면은 코드에서 렌더한 정적 프리뷰입니다. 실제 배경과 움직임이 있는 상황에서도 잘 읽히는지는 플레이 검증으로 계속 확인해야 합니다.',source:'Scripts/UI/UiKit.cs, HudPresentation.cs, Scripts/InGame/StageClearCelebration.cs. 화면은 UiUpgradeWork/Verification/result-1280x720.png.'},
{title:'현재 성과와 다음 검증',seconds:35,text:'현재는 회피와 시간 정지, 다섯 지역의 콘텐츠 등록, 맵 제작 도구를 연결한 상태입니다. 저장에는 백업 복구를, 진행에는 체크포인트와 최고 기록을 갖췄습니다. 다음 단계는 스물다섯 개 맵의 실제 클리어 경로, 보스와 지역 간 난이도, 긴 플레이 중 UI와 성능을 확인하는 것입니다. 이 프로젝트의 성과는 특징적인 플레이와 콘텐츠를 반복 제작하는 구조를 함께 만들었다는 점입니다. 감사합니다.',source:'Scripts/Services/SaveService.cs, JsonFileStore.cs, Scripts/InGame/StageSession.cs, Scripts/Data/SaveData.cs. 현재 자료는 코드·데이터·씬 정적 점검과 기존 UI 프리뷰 기반입니다. 전체 플레이테스트 또는 FPS 측정 결과를 주장하지 않습니다.'}
];
function note(s,index){const n=notes[index];s.speakerNotes.text=`[${n.seconds}초]\n${n.text}\n\n${n.cue??''}\n\n근거: ${n.source}`;}
export async function build({presentation:p,input}){
 if(input.fixCover){
   const first=p.slides.getItem(0);if(first.shapes.items.length===0&&first.images.items.length===0)p.slides.delete(0);
   for(const [i,sh] of p.slides.getItem(0).shapes.items.entries())sh.text.style={typeface:'Noto Sans KR',fontSize:[64,36,22,18][i]??22,color:i<2?C.white:i===2?C.gold:C.muted,bold:false,insets:0};
   note(p.slides.getItem(0),0);
 }
 for(const n of input.slides??[]){
 let s;
 if(n===2){s=base(p,2,notes[1].title);image(s,PRE+'title-ko-1280x720.png',64,185,760,428);text(s,'25',890,172,230,96,76,C.gold);text(s,'일반 20개 + 보스 5개',892,267,315,48,25);line(s,890,333,320);text(s,'30',890,356,130,80,62,C.gold);text(s,'몬스터 구현 등록',892,438,320,45,25);text(s,'5개 언어 지원',892,516,320,60,30);text(s,'월드맵 선택  /  스테이지 도전  /  기록·수집 결과',64,620,1090,44,23,C.muted);text(s,'콘텐츠 수는 등록 기준입니다. 전체 플레이 검증 완료를 뜻하지 않습니다.',64,669,1060,28,17,C.muted);}
 if(n===3){s=base(p,3,notes[2].title);text(s,'2초',64,183,380,156,112,C.gold);text(s,'세계는 멈추고\n플레이어는 계속 움직인다',64,355,470,130,34);text(s,'회피 가능한 공격과\n회피 동작이 겹칠 때 발동',64,535,460,82,24,C.muted);
 const ys=[210,350,490],names=['공격에 맞춰 회피','플레이어만 이동','원래 상태 복구'],bodies=['피해 회피와 시간 정지 발동','수동 물리 계산과 별도 애니메이션','월드 속도·물리·음악 복원'];
 for(let i=0;i<3;i++){text(s,'0'+(i+1),620,ys[i],80,58,33,C.gold);text(s,names[i],720,ys[i],480,54,31);text(s,bodies[i],720,ys[i]+58,485,50,22,C.muted);if(i<2)line(s,620,ys[i]+119,580);}
 text(s,'흑백 화면과 시계 소리로 상태 전달',620,651,570,32,20,C.blue);}
 if(n===4){s=base(p,4,notes[3].title);const labels=['공격 예고','패턴 회피','약점 노출','밟기 반격'];for(let i=0;i<4;i++){const x=64+i*298;text(s,'0'+(i+1),x,201,240,64,38,C.gold);text(s,labels[i],x,280,240,62,34);rect(s,x,365,244,4,i===2?C.gold:C.line);}text(s,'돌진  /  충격파  /  낙하 공격  /  용암 분출',64,428,1140,70,30);text(s,'보스별 패턴 데이터와 외형을 공통 전투 로직에 연결',64,540,1140,72,28,C.muted);text(s,'공격 뒤의 기절 구간을 반격 기회로 사용',64,635,1080,38,23,C.gold);}
 if(n===5){s=base(p,5,notes[4].title);const names=['초원','사막','설산','산호 해안','용암 요새'],colors=['#9ABF76','#D8AE69','#A9D8ED','#EA9C8C','#E89167'],desc=['기본 점프\n밟기 공격','가시 트랩\n스위치·게이트','얼음 바닥\n미끄러지는 제동','바위섬 경로\n이동 발판','발판 타이밍\n순차 분출 보스'];for(let i=0;i<5;i++){const x=64+i*235;rect(s,x,210,210,8,colors[i]);text(s,'0'+(i+1),x,250,210,85,54,colors[i]);text(s,names[i],x,348,215,62,31);text(s,desc[i],x,449,216,110,25,C.muted);}text(s,'지역마다 일반 스테이지 4개와 보스 스테이지 1개 등록',64,627,1130,48,27);}
 if(n===6){s=base(p,6,notes[5].title);const steps=['배치 편집','JSON 저장','규칙 검증','씬 생성'];for(let i=0;i<4;i++){const x=64+i*298;text(s,String(i+1),x,190,250,70,44,C.gold);text(s,steps[i],x,274,250,65,34);rect(s,x,360,244,3,C.line);}text(s,'제작 편의',64,420,480,56,31,C.gold);text(s,'그리드·표면 맞춤\n복제와 실행 취소',64,493,490,110,29);text(s,'같은 규칙으로 검증',665,420,530,56,31,C.gold);text(s,'중복 ID·참조·블록 겹침\n기본 거리·높이 경고',665,493,535,110,29);text(s,'AI·외부 도구도 자동화 API로 연결 가능. 실제 클리어 가능성은 플레이 검증이 필요합니다.',64,657,1130,35,19,C.muted);}
 if(n===7){s=base(p,7,notes[6].title);image(s,PRE+'result-1280x720.png',64,175,825,464);text(s,'Noto Sans',928,207,290,66,30,C.gold);text(s,'용도별 크기·굵기\n공통 정렬 규칙',928,287,290,100,25);line(s,928,425,286);text(s,'시간 / 코인 / 수집품',928,453,290,85,26);text(s,'정보 구획과 배경 대비',928,550,290,66,23,C.muted);text(s,'코드 기반 UI 정적 프리뷰. 실제 플레이 장면의 가독성 검증은 별도입니다.',64,663,1100,30,18,C.muted);}
 if(n===8){s=base(p,8,notes[7].title);text(s,'현재 갖춘 것',64,198,520,64,34,C.gold);text(s,'회피와 시간 정지\n5지역 콘텐츠 등록\n맵 편집·검증·씬 생성\n기록 저장과 백업 복구',64,297,530,268,30);text(s,'다음에 확인할 것',700,198,515,64,34,C.blue);text(s,'25개 맵의 실제 클리어 경로\n지역·보스 난이도 연결\n플레이 중 UI 가독성\n성능과 장시간 플레이 안정성',700,297,520,268,29);line(s,64,613,1152);text(s,'특징적인 플레이와 콘텐츠를 반복 제작하는 구조',64,637,1130,49,30,C.white);}
 note(s,n-1);
 }
 return {slides:p.slides.items.length};
}
