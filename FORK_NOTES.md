# roycho96 Multiplayer fork

이 브랜치는 upstream dev `ddfccdbc9538724fd1dd97146e81ddae8fecef69`를 기준으로 한다. 기존 공개 fork의 수정과 미커밋 수정을 기능별로 검토해 다시 구성했다.

표시 버전은 `0.11.5-roycho96.2`, network/replay protocol은 `57009602`이다. 모든 플레이어는 같은 본체와 외부 모드 조합을 사용해야 한다. Compat을 사용한다면 `ForkCompatibilityRevision = 1`인 roycho96 fork와 함께 사용한다. 본체 단독 실행도 지원한다.

## 주요 변경

- 발신자의 SelectedPlan을 command payload에 포함하고 중첩·예외 후 UI context를 복구한다.
- ThingComp 역직렬화에서 잘못된 부모·타입·index를 처리하면서 다음 필드의 reader 위치를 유지한다.
- 물고기 그림자를 유지하며 셀 선택을 안정화한다. 고장 판정과 전력 계산은 실제 소비 목록을 MP 시뮬레이션에서 정렬한다.
- storage filter·설치 대상·gravship marker를 확인하고 원래 다중 셀 Designator 동작을 유지한다.
- 건설 workDone의 기존 반올림은 MP에만 적용한다. 모든 stat의 반올림이나 cross-CPU 결정성 보장은 포함하지 않는다.
- Windows arbiter는 `-batchmode`로 창을 숨겨 실행하면서 그래픽 초기화를 허용한다. 1.6.4871에서 `-nographics`로 인한 texture atlas 초기화 오류를 재현했기 때문이다. 다른 운영체제의 기존 옵션은 유지하며, Windows arbiter에는 그래픽 장치와 추가 graphics memory가 필요하다.
- Compat의 광범위한 vanilla tick RNG 격리, 전역 Job ID rollback, 일반 NRE 억제는 포함하지 않는다.

## 기존 게임 이동

이전 protocol의 replay 명령은 새 버전으로 직접 실행하지 않는다. 원래 모드 조합으로 게임을 열고 single-player `.rws`로 저장한 뒤 새 조합으로 host한다. 기존 저장 상태에 이미 존재하는 ID 충돌이나 desync를 자동으로 복구하는 작업은 아니다.

standalone 서버도 archive 및 지속 저장 상태의 protocol을 확인한다. 새 snapshot은 `Saved/protocol.bin`에 protocol을 기록한다. 이전 protocol이나 출처를 확인할 수 없는 상태를 조용히 읽지 않는다.

## 빌드

`global.json`에 지정된 .NET 10 SDK가 필요하다. Source generator는 upstream Roslyn 패키지 `5.3.0`을 사용한다. 게임 참조는 `1.6.4871`, Harmony는 `2.4.2`이다.

```sh
dotnet build Source/Client/Multiplayer.csproj -c Release
dotnet test Source/Tests/Tests.csproj -c Release
dotnet build Source/Server/Server.csproj -c Release
```

WSL에서 dotnet이 PATH에 없다면 `~/.dotnet/dotnet`을 사용한다. 클라이언트 출력인 `Assemblies/`, `AssembliesCustom/`와 `About/`, `Defs/`, `Languages/`, `Textures/`를 함께 사용한다. `Languages` submodule은 `git submodule update --init Languages`로 준비한다.

Workshop ID `3703892373`과 packageId `rwmt.Multiplayer`를 유지한다. 같은 packageId의 원본 Workshop 모드와 동시에 활성화하지 않는다. 기존 upstream 문서와 라이선스는 그대로 유지한다.
