# TripleSort_Portfolio - 매치3 퍼즐 게임

##프로젝트 개요
모바일 기반의 매치3 퍼즐 게임입니다.
이 프로젝트는 프로그래머 1명, 디자이너 1명, 기획자 1명으로 전체 게임 구조를 직접 설계하고 구현했습니다.

##사용 기술
Unity 6000.0.36f1
UniTask, Zenject
Addressables, TextMeshPro
Git, GitHub

##프로젝트 구조
```bash
Assets/
├── 00_Framework/
├── 01_Addressable/
├── 02_Game/
│ ├── Prefabs/
│ ├── Scenes/
│ ├── Scripts/
│ │ ├── Edit/
│ │ │ ├── MapEditSystem.cs # 맵 에디터 시스템
│ │ │ └── MapEditSystem.Stage.cs # 맵 에디터 - 스테이지 관련
│ │ ├── Editor/
│ │ │ ├── MapEditorWindow.cs # 맵 에디터 창
│ │ │ ├── MapEditorWindow.Stage.cs # 맵 에디터 - 스테이지 관련
│ │ │ └── ObjectChangeEventTracker.cs # 씬 오브젝트 변경 감지 이벤트 트래커
│ │ ├── Game/
│ │ │ ├── BasicBox/
│ │ │ │ └── Box/
│ │ │ │ ├── BoxController.cs # 상자 로직
│ │ │ │ ├── BoxFactory.cs # 상자 대여 관리
│ │ │ │ ├── BoxModel.cs # 상자 데이터
│ │ │ │ └── BoxView.cs # 상자 뷰
│ │ │ ├── Combo/
│ │ │ ├── System/
│ │ │ │ ├── StageSystem.cs # 스테이지 생성, 초기화, 진행 및 클리어 조건 관리
│ │ │ │ └── UserSystem.cs # 유저 데이터 관리
│ │ │ └── Thing/ # 상품 관련
│ │ ├── Popup/
│ │ ├── Scene/
│ │ └── Shader/
│ ├── Spins/
│ └── Sprites/
├── 03_Effects/
└── Scenes/
```