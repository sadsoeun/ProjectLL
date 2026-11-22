# Leaf & Leap

황금 나뭇잎을 찾아 다섯 세계를 뛰어넘는 3D 횡스크롤 플랫포머입니다.

- 엔진: Unity 6.3 LTS (6000.3.6f1), Universal Render Pipeline
- 플랫폼: Windows (전체 게임), 웹 브라우저 (월드 1 데모)

## 웹 데모

브라우저에서 바로 플레이할 수 있는 데모입니다. 튜토리얼과 월드 1(스테이지 1-1 ~ 1-보스)까지 들어 있습니다.

- 주소: `https://<GitHub 아이디>.github.io/<저장소 이름>/`
- 데스크톱 브라우저(Chrome, Edge)를 권장합니다. 처음 접속할 때 약 450MB를 내려받습니다.
- 웹 데모에서는 Steam 온라인 랭킹과 녹음실(마이크 녹음)이 동작하지 않습니다. 랭킹 화면에는 내 기록만 표시됩니다.

## 조작

| 동작 | 키보드 |
|---|---|
| 이동 | A / D 또는 ← / → |
| 점프 | Space |
| 달리기 | Ctrl |
| 메뉴 | Esc |

게임패드도 지원합니다.

## 프로젝트 열기

1. Unity Hub에서 Unity 6000.3.6f1로 이 폴더를 엽니다.
2. `Assets/_Project/Scenes/01_Logo.unity`에서 플레이를 시작합니다.

Steam 랭킹은 Steam 클라이언트가 실행 중이고 로그인돼 있을 때 동작합니다. 전용 App ID가 나오기 전까지는 Valve의 테스트 App ID(480, `steam_appid.txt`)를 사용합니다.

## 웹 데모 다시 만들기

1. Unity 메뉴 `Tools > MyGame > Build Web Demo`를 실행합니다. (`Assets/_Project/Scripts/Editor/WebDemoBuild.cs`)
2. `python Tools/pack_web_demo.py <빌드 폴더> <사이트 폴더>`를 실행합니다. GitHub의 파일당 100MB 제한에 맞춰 데이터 파일을 나누고 실행용 페이지를 만듭니다.
3. 사이트 폴더의 내용을 `gh-pages` 브랜치에 올립니다.

## 라이선스와 사용 자산

- 플레이어 캐릭터는 유니티짱(Unity-chan)을 사용합니다. © Unity Technologies Japan/UCL. 유니티짱 라이선스 조항에 따라 제공됩니다. (`Assets/unity-chan!/Unity-chan! Model/Documentation/UnityChanLicense2.0`)
- Live2D Cubism SDK for Unity: `Assets/Live2D/Cubism/LICENSE.md`의 조건을 따릅니다.
- Steamworks.NET: MIT License.
- 위 항목을 제외한 이 저장소의 게임 코드와 자산은 제작자의 것이며, 별도 표기가 없는 한 재사용 허가를 뜻하지 않습니다.
