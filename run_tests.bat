@echo off
setlocal enabledelayedexpansion
set FAILED=0
set TOTAL=0

set TESTS=TestStep4Bridge.exe TestNaturalLanguagePromptSpawner.exe TestAssetDiscoveryPipeline.exe TestEpistemicAssetIntelligence.exe TestSpatialArchitecture.exe TestEndToEndIntegration.exe TestMilestone3Mutations.exe TestPlanSerializationRoundtrip.exe TestPlanRepository.exe TestEnvironmentStateArchitecture.exe TestMilestone2Environments.exe TestCommissioningArchitecture.exe TestRuntimeOwnershipArchitecture.exe TestRealizationLifecycleArchitecture.exe TestNpcDeathObservationArchitecture.exe TestWriteBeforeDestroyInvariant.exe TestRealizationScopeRollbackArchitecture.exe TestSeparatedWorldDirectorArchitecture.exe TestContextRetrievalArchitecture.exe TestEngramResolutionArchitecture.exe TestModularGeneratorArchitecture.exe TestSalienceEvaluationArchitecture.exe TestEphemeralStateArchitecture.exe TestKairosInsightArchitecture.exe TestSpatialPerceptionArchitecture.exe TestSpatialWorldModelArchitecture.exe TestSpatialOrchestrationPipeline.exe

for %%T in (%TESTS%) do (
    set /a TOTAL+=1
    %%T > nul
    if !ERRORLEVEL! EQU 0 (
        echo [PASS] %%T
    ) else (
        echo [FAIL] %%T
        set /a FAILED+=1
    )
)

echo.
echo ========================================================
echo   SUMMARY: %TOTAL% test suites executed, %FAILED% failed.
echo ========================================================

if %FAILED% EQU 0 (
    exit /b 0
) else (
    exit /b 1
)
