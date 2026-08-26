# Build and deploy

이 문서는 Spell Thrower를 Unity WebGL로 빌드하고 GitHub Pages에 배포하는 개발자용 안내입니다. 심사자는 [README.md](README.md)의 플레이 링크만 사용하면 됩니다.

## 요구 사항

- Unity `6000.3.22f1`
- Unity Hub의 **WebGL Build Support** 모듈
- 빌드 중 프로젝트 파일을 잠그지 않도록 Unity Editor 종료

## WebGL 빌드

프로젝트 루트에서 Unity 경로를 환경에 맞게 바꿔 실행합니다.

```bash
UNITY=/path/to/Unity
"$UNITY" \
  -batchmode -nographics -quit \
  -projectPath "$PWD" \
  -buildTarget WebGL \
  -executeMethod SpellThrower.BuildTools.WebGLBuild.Build \
  -logFile Logs/webgl-build.log
```

Linux에서는 Unity 설치 경로를 환경에 맞게 지정해 실행합니다.

```bash
UNITY=/path/to/Unity/Hub/Editor/6000.3.22f1/Editor/Unity
"$UNITY" \
  -batchmode -nographics -quit \
  -projectPath "$PWD" \
  -buildTarget WebGL \
  -executeMethod SpellThrower.BuildTools.WebGLBuild.Build \
  -logFile Logs/webgl-build.log
```

빌드 산출물은 `WebGLBuild/`에 생성됩니다. 빌드 후 스크립트가 심사용 캔버스 렌더 타깃을 1920×1080으로 고정하고 `.nojekyll` 파일도 생성합니다.

## GitHub Pages 배포

`main` 브랜치에 `WebGLBuild/` 변경을 push하면 `.github/workflows/deploy-webgl.yml`이 자동 실행됩니다. Workflow는 다음 파일을 확인한 뒤 `WebGLBuild/` 전체를 Pages 아티팩트로 배포합니다.

- `WebGLBuild/index.html`
- `WebGLBuild/Build/`
- `WebGLBuild/.nojekyll`

현재 공개 페이지는 다음 주소입니다.

https://ganyu-agent.github.io/SpellThrower-WebGL/

WebGL 브라우저는 UDP 소켓을 사용할 수 없으므로 공개 빌드는 Unity Relay의 보안 WebSocket(WSS) 경로를 사용합니다.
