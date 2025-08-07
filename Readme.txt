본 파일은 DTP7 티칭팬던트에 하드웨어 연동 중 초기 파일임

HardwareControllers 라는 폴더를 만들고있었으며
TeachingPendant.proj 파일을 메모장으로 수정하여

<Compile Include="HardwareControllers\DTP7HCommunication.cs" />

라는 컴파일 문구를 집어넣었음.
추가로, 해당 컴파일 문구는 본래

<Compile Include="ErrorHandling\ErrorRecovery.cs" />
    <Compile Include="ErrorHandling\GlobalExceptionHandler.cs" />
    <Compile Include="HardwareControllers\DTP7HCommunication.cs" />
    <Compile Include="HardwareControllers\IRobotController.cs" />
    <Compile Include="Logging\FileLogWriter.cs" />
    <Compile Include="Logging\LogEntry.cs" />
    <Compile Include="Logging\Logger.cs" />
    <Compile Include="Logging\LogLevel.cs" />
    <Compile Include="Logging\LogManager.cs" />
    <Compile Include="Manager\GlobalModeManager.cs" />
    <Compile Include="Manager\GlobalSpeedManager.cs" />
    <Compile Include="Manager\IOController.cs" />
    <Compile Include="Manager\PersistentDataManager.cs" />
    <Compile Include="ModeChangedEventArgs.cs" />
    <Compile Include="MonitorUI\Monitor.xaml.cs">
      <DependentUpon>Monitor.xaml</DependentUpon>
    </Compile>
    <Compile Include="Movement\MovementUI\MovementDataHelper.cs" />
    <Compile Include="Movement\MovementUI\MovementHomePosIntegration.cs" />
    <Compile Include="Movement\MovementUI\PhysicsTestResultWindow.xaml.cs">
      <DependentUpon>PhysicsTestResultWindow.xaml</DependentUpon>
    </Compile>
    <Compile Include="RemoteControlWindow.xaml.cs">
      <DependentUpon>RemoteControlWindow.xaml</DependentUpon>
    </Compile>
    <Compile Include="Safety\SafetySystem.cs" />
    <Compile Include="Setup\Setup.xaml.cs">
      <DependentUpon>Setup.xaml</DependentUpon>
    </Compile>

이렇게 되어있었음.
또한 HardwareControllers 폴더의 파일은 DTP7HCommunication.cs, IRobotController.cs 두개이며, 둘 다 미완성 파일이니 혹시 롤백한다면
HardwareControllers 폴더 자체를 삭제할것