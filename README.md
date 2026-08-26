# Spell Thrower

브라우저에서 두 명이 대전하는 턴제 카드 게임입니다.

## 플레이 링크

https://ganyu-agent.github.io/SpellThrower-WebGL/

## 플레이 방법

1. 동일 LAN의 두 브라우저 창 또는 서로 다른 두 기기에서 플레이 링크를 엽니다.
2. **START**를 누르고 닉네임을 등록합니다.
3. **DECK**에서 25장 덱을 준비한 뒤 로비로 돌아갑니다.
4. 두 플레이어 모두 **MATCH**를 누릅니다.
5. 자기 턴에 손패의 카드를 드래그해 강조된 보드 칸에 놓습니다.
6. 행동을 마치면 **END TURN**을 누릅니다.

별도 설치와 게임 계정 로그인은 필요하지 않습니다. 매칭에는 Unity Gaming
Services의 익명 인증과 Relay가 사용됩니다.

## WebGL 빌드

Unity `6000.3.22f1`과 **WebGL Build Support** 모듈이 필요합니다. Unity
에디터를 닫은 상태에서 프로젝트 루트에서 다음 명령으로 빌드할 수 있습니다.

```powershell
/home/forestpenguin/Unity/Hub/Editor/6000.3.22f1/Editor/Unity \
  -batchmode -nographics -quit \
  -projectPath "$PWD" \
  -buildTarget WebGL \
  -executeMethod SpellThrower.BuildTools.WebGLBuild.Build \
  -logFile Logs/webgl-build.log
```

출력은 `WebGLBuild/`에 생성됩니다. `main`에 푸시하면 Pages 워크플로가
공개 플레이 링크를 갱신합니다. WebGL에서는 브라우저가 UDP를 열 수 없으므로
Unity Relay의 WSS 경로를 사용합니다.
