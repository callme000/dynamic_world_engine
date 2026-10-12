@echo off
echo =========================================================
echo   COMPILING DYNAMIC WORLD ENGINE (SHVDN PLUGIN)
echo =========================================================

if not exist "dist" mkdir dist

C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:library /out:dist\DynamicWorldEngine.dll /r:".\lib\ScriptHookVDotNet3.dll",".\lib\Newtonsoft.Json.dll","System.Windows.Forms.dll" src\Utils\ClipboardHelper.cs src\Utils\EngineLogger.cs src\Discovery\GtaInstallationLocator.cs src\Discovery\DlcPackDiscoveryEngine.cs src\Discovery\GameAssetScanner.cs src\Discovery\DynamicInteriorDiscoveryProvider.cs src\Discovery\OpenWorldLocationProvider.cs src\Discovery\SemanticClassificationEngine.cs src\Discovery\CanonicalIdentityResolver.cs src\Discovery\GenericPrimitiveComposer.cs src\Discovery\AssetIndexCacheManager.cs src\Discovery\SemanticDecomposer.cs src\Database\Models.cs src\Database\WorldMemoryStore.cs src\Database\EvidenceRecord.cs src\Database\CanonicalCharacterRegistry.cs src\Database\SpatialAssetRegistry.cs src\Commands\ActionSchemas.cs src\Commands\MutationSchemas.cs src\Commands\ActionParser.cs src\Commands\WorldActionExecutor.cs src\Archetypes\EnvironmentCatalog.cs src\Audio\PartyAudioManager.cs src\Services\JsonRepairUtility.cs src\Services\PromptCache.cs src\Services\LlmTransportClient.cs src\Services\LocalWorldSynthesizer.cs src\Services\WorldGeneratorService.cs src\Spatial\LocationSelectionResolver.cs src\Spatial\SpatialExecutionPlan.cs src\Spatial\PlanRepository.cs src\Spatial\VenueSelectionResolver.cs src\Spatial\SpatialFeasibilityEvaluator.cs src\Spatial\ProceduralCrowdDirector.cs src\Spatial\SpatialAffordanceScanner.cs src\Spatial\SpatialVisionEngine.cs src\Spatial\SpatialQueryService.cs src\Spatial\PropPlacementEngine.cs src\Spatial\HierarchicalTransformEngine.cs src\Spatial\ModularAssetCatalog.cs src\Spatial\NavMeshBlockerManager.cs src\Spatial\TeleportPocketManager.cs src\Spatial\InteriorCatalog.cs src\Spatial\InteriorManager.cs src\Spatial\InteriorActivationJob.cs src\Spatial\SpatialWorldPlanner.cs src\Spatial\BlipManager.cs src\Spatial\WorldPerceptionSnapshot.cs src\Spatial\SpatialPerceptionScanner.cs src\Spatial\SpatialWorldModel.cs src\Spatial\SpatialGridSampler.cs src\Spatial\SpatialWorldModelRenderer.cs src\Spatial\SpatialPlacementSolver.cs src\Spatial\WorldContextPacket.cs src\Spatial\SpatialPlanValidator.cs src\Runtime\EntitySpawnerService.cs src\Runtime\RealizationScope.cs src\Runtime\RuntimeExecutor.cs src\Runtime\PlayerTeleportCoordinator.cs src\Runtime\EntityMutationHandler.cs src\Runtime\WorldEventDispatcher.cs src\Simulation\ActiveNpcContext.cs src\Simulation\NpcActivityDirector.cs src\Simulation\IntentInterpreter.cs src\Simulation\NpcStateMachine.cs src\Simulation\DirectorTimelineManager.cs src\Simulation\SalienceEvaluator.cs src\Simulation\EphemeralStateRecomputer.cs src\Simulation\WorldSimulationEngine.cs src\Observer\WorldObserver.cs src\Retrieval\ContextRetriever.cs src\Retrieval\EngramResolver.cs src\Retrieval\KairosInsightCompiler.cs src\UI\WorldInspector.cs src\Bridge\LivingLsAiBridge.cs src\EngineCore.cs src\DynamicWorldEngineScript.cs


if %ERRORLEVEL% EQU 0 (
    echo.
    echo =========================================================
    echo   BUILD SUCCESSFUL! Created dist\DynamicWorldEngine.dll
    echo =========================================================
    copy /Y ".\lib\Newtonsoft.Json.dll" "dist\"
    copy /Y "dist\DynamicWorldEngine.dll" ".\"
    if not exist "dist\DynamicWorldEngineData" mkdir "dist\DynamicWorldEngineData"
    copy /Y "DynamicWorldEngineData\*.json" "dist\DynamicWorldEngineData\"
    if not exist "dist\DynamicWorldEngineData\environments" mkdir "dist\DynamicWorldEngineData\environments"
    copy /Y "DynamicWorldEngineData\environments\*.json" "dist\DynamicWorldEngineData\environments\"
    if not exist "dist\DynamicWorldEngineData\plans" mkdir "dist\DynamicWorldEngineData\plans"
    if exist "C:\Program Files\Epic Games\GTAVEnhanced\scripts" (
        copy /Y "dist\DynamicWorldEngine.dll" "C:\Program Files\Epic Games\GTAVEnhanced\scripts\"
        copy /Y ".\lib\Newtonsoft.Json.dll" "C:\Program Files\Epic Games\GTAVEnhanced\scripts\"
        if not exist "C:\Program Files\Epic Games\GTAVEnhanced\scripts\DynamicWorldEngineData" mkdir "C:\Program Files\Epic Games\GTAVEnhanced\scripts\DynamicWorldEngineData"
        copy /Y "DynamicWorldEngineData\*.json" "C:\Program Files\Epic Games\GTAVEnhanced\scripts\DynamicWorldEngineData\"
        if not exist "C:\Program Files\Epic Games\GTAVEnhanced\scripts\DynamicWorldEngineData\environments" mkdir "C:\Program Files\Epic Games\GTAVEnhanced\scripts\DynamicWorldEngineData\environments"
        copy /Y "DynamicWorldEngineData\environments\*.json" "C:\Program Files\Epic Games\GTAVEnhanced\scripts\DynamicWorldEngineData\environments\"
        if not exist "C:\Program Files\Epic Games\GTAVEnhanced\scripts\DynamicWorldEngineData\plans" mkdir "C:\Program Files\Epic Games\GTAVEnhanced\scripts\DynamicWorldEngineData\plans"
    )
    if exist "C:\Users\melod\Desktop\mod backup\scripts" (
        copy /Y "dist\DynamicWorldEngine.dll" "C:\Users\melod\Desktop\mod backup\scripts\"
        copy /Y ".\lib\Newtonsoft.Json.dll" "C:\Users\melod\Desktop\mod backup\scripts\"
        if not exist "C:\Users\melod\Desktop\mod backup\scripts\DynamicWorldEngineData" mkdir "C:\Users\melod\Desktop\mod backup\scripts\DynamicWorldEngineData"
        copy /Y "DynamicWorldEngineData\*.json" "C:\Users\melod\Desktop\mod backup\scripts\DynamicWorldEngineData\"
        if not exist "C:\Users\melod\Desktop\mod backup\scripts\DynamicWorldEngineData\environments" mkdir "C:\Users\melod\Desktop\mod backup\scripts\DynamicWorldEngineData\environments"
        copy /Y "DynamicWorldEngineData\environments\*.json" "C:\Users\melod\Desktop\mod backup\scripts\DynamicWorldEngineData\environments\"
        if not exist "C:\Users\melod\Desktop\mod backup\scripts\DynamicWorldEngineData\plans" mkdir "C:\Users\melod\Desktop\mod backup\scripts\DynamicWorldEngineData\plans"
    )
    echo Building TestNaturalLanguagePromptSpawner.exe...
    C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:exe /out:TestNaturalLanguagePromptSpawner.exe /r:".\dist\DynamicWorldEngine.dll",".\lib\Newtonsoft.Json.dll",".\lib\ScriptHookVDotNet3.dll" tests\TestNaturalLanguagePromptSpawner.cs
    echo Building TestAssetDiscoveryPipeline.exe...
    C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:exe /out:TestAssetDiscoveryPipeline.exe /r:".\dist\DynamicWorldEngine.dll",".\lib\Newtonsoft.Json.dll",".\lib\ScriptHookVDotNet3.dll" tests\TestAssetDiscoveryPipeline.cs
    echo Building TestEpistemicAssetIntelligence.exe...
    C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:exe /out:TestEpistemicAssetIntelligence.exe /r:".\dist\DynamicWorldEngine.dll",".\lib\Newtonsoft.Json.dll",".\lib\ScriptHookVDotNet3.dll" tests\TestEpistemicAssetIntelligence.cs
    echo Building TestSpatialArchitecture.exe...
    C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:exe /out:TestSpatialArchitecture.exe /r:".\dist\DynamicWorldEngine.dll",".\lib\Newtonsoft.Json.dll",".\lib\ScriptHookVDotNet3.dll" tests\TestSpatialArchitecture.cs
    echo Building TestEndToEndIntegration.exe...
    C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:exe /out:TestEndToEndIntegration.exe /r:".\dist\DynamicWorldEngine.dll",".\lib\Newtonsoft.Json.dll",".\lib\ScriptHookVDotNet3.dll" tests\TestEndToEndIntegration.cs
    echo Building TestStep4Bridge.exe...
    C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:exe /out:TestStep4Bridge.exe /r:".\dist\DynamicWorldEngine.dll",".\lib\Newtonsoft.Json.dll",".\lib\ScriptHookVDotNet3.dll" tests\TestStep4Bridge.cs
    echo Building TestMilestone3Mutations.exe...
    C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:exe /out:TestMilestone3Mutations.exe /r:".\dist\DynamicWorldEngine.dll",".\lib\Newtonsoft.Json.dll",".\lib\ScriptHookVDotNet3.dll" tests\TestMilestone3Mutations.cs
    echo Building TestPlanSerializationRoundtrip.exe...
    C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:exe /out:TestPlanSerializationRoundtrip.exe /r:".\dist\DynamicWorldEngine.dll",".\lib\Newtonsoft.Json.dll",".\lib\ScriptHookVDotNet3.dll" tests\TestPlanSerializationRoundtrip.cs
    echo Building TestPlanRepository.exe...
    C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:exe /out:TestPlanRepository.exe /r:".\dist\DynamicWorldEngine.dll",".\lib\Newtonsoft.Json.dll",".\lib\ScriptHookVDotNet3.dll" tests\TestPlanRepository.cs
    echo Building TestEnvironmentStateArchitecture.exe...
    C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:exe /out:TestEnvironmentStateArchitecture.exe /r:".\dist\DynamicWorldEngine.dll",".\lib\Newtonsoft.Json.dll",".\lib\ScriptHookVDotNet3.dll" tests\TestEnvironmentStateArchitecture.cs
    echo Building TestMilestone2Environments.exe...
    C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:exe /out:TestMilestone2Environments.exe /r:".\dist\DynamicWorldEngine.dll",".\lib\Newtonsoft.Json.dll",".\lib\ScriptHookVDotNet3.dll" tests\TestMilestone2Environments.cs
    echo Building TestCommissioningArchitecture.exe...
    C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:exe /out:TestCommissioningArchitecture.exe /r:".\dist\DynamicWorldEngine.dll",".\lib\Newtonsoft.Json.dll",".\lib\ScriptHookVDotNet3.dll" tests\TestCommissioningArchitecture.cs
    echo Building TestRuntimeOwnershipArchitecture.exe...
    C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:exe /out:TestRuntimeOwnershipArchitecture.exe /r:".\dist\DynamicWorldEngine.dll",".\lib\Newtonsoft.Json.dll",".\lib\ScriptHookVDotNet3.dll" tests\TestRuntimeOwnershipArchitecture.cs
    echo Building TestRealizationLifecycleArchitecture.exe...
    C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:exe /out:TestRealizationLifecycleArchitecture.exe /r:".\dist\DynamicWorldEngine.dll",".\lib\Newtonsoft.Json.dll",".\lib\ScriptHookVDotNet3.dll" tests\TestRealizationLifecycleArchitecture.cs
    echo Building TestNpcDeathObservationArchitecture.exe...
    C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:exe /out:TestNpcDeathObservationArchitecture.exe /r:".\dist\DynamicWorldEngine.dll",".\lib\Newtonsoft.Json.dll",".\lib\ScriptHookVDotNet3.dll" tests\TestNpcDeathObservationArchitecture.cs
    echo Building TestWriteBeforeDestroyInvariant.exe...
    C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:exe /out:TestWriteBeforeDestroyInvariant.exe /r:".\dist\DynamicWorldEngine.dll",".\lib\Newtonsoft.Json.dll",".\lib\ScriptHookVDotNet3.dll" tests\TestWriteBeforeDestroyInvariant.cs
    echo Building TestRealizationScopeRollbackArchitecture.exe...
    C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:exe /out:TestRealizationScopeRollbackArchitecture.exe /r:".\dist\DynamicWorldEngine.dll",".\lib\Newtonsoft.Json.dll",".\lib\ScriptHookVDotNet3.dll" tests\TestRealizationScopeRollbackArchitecture.cs
    echo Building TestSeparatedWorldDirectorArchitecture.exe...
    C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:exe /out:TestSeparatedWorldDirectorArchitecture.exe /r:".\dist\DynamicWorldEngine.dll",".\lib\Newtonsoft.Json.dll",".\lib\ScriptHookVDotNet3.dll" tests\TestSeparatedWorldDirectorArchitecture.cs
    echo Building TestContextRetrievalArchitecture.exe...
    C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:exe /out:TestContextRetrievalArchitecture.exe /r:".\dist\DynamicWorldEngine.dll",".\lib\Newtonsoft.Json.dll",".\lib\ScriptHookVDotNet3.dll" tests\TestContextRetrievalArchitecture.cs
    echo Building TestEngramResolutionArchitecture.exe...
    C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:exe /out:TestEngramResolutionArchitecture.exe /r:".\dist\DynamicWorldEngine.dll",".\lib\Newtonsoft.Json.dll",".\lib\ScriptHookVDotNet3.dll" tests\TestEngramResolutionArchitecture.cs
    echo Building TestModularGeneratorArchitecture.exe...
    C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:exe /out:TestModularGeneratorArchitecture.exe /r:".\dist\DynamicWorldEngine.dll",".\lib\Newtonsoft.Json.dll",".\lib\ScriptHookVDotNet3.dll" tests\TestModularGeneratorArchitecture.cs
    echo Building TestSalienceEvaluationArchitecture.exe...
    C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:exe /out:TestSalienceEvaluationArchitecture.exe /r:".\dist\DynamicWorldEngine.dll",".\lib\Newtonsoft.Json.dll",".\lib\ScriptHookVDotNet3.dll" tests\TestSalienceEvaluationArchitecture.cs
    echo Building TestEphemeralStateArchitecture.exe...
    C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:exe /out:TestEphemeralStateArchitecture.exe /r:".\dist\DynamicWorldEngine.dll",".\lib\Newtonsoft.Json.dll",".\lib\ScriptHookVDotNet3.dll" tests\TestEphemeralStateArchitecture.cs
    echo Building TestKairosInsightArchitecture.exe...
    C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:exe /out:TestKairosInsightArchitecture.exe /r:".\dist\DynamicWorldEngine.dll",".\lib\Newtonsoft.Json.dll",".\lib\ScriptHookVDotNet3.dll" tests\TestKairosInsightArchitecture.cs
    echo Building TestSpatialPerceptionArchitecture.exe...
    C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:exe /out:TestSpatialPerceptionArchitecture.exe /r:".\dist\DynamicWorldEngine.dll",".\lib\Newtonsoft.Json.dll",".\lib\ScriptHookVDotNet3.dll" tests\TestSpatialPerceptionArchitecture.cs
    echo Building TestSpatialWorldModelArchitecture.exe...
    C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:exe /out:TestSpatialWorldModelArchitecture.exe /r:".\dist\DynamicWorldEngine.dll",".\lib\Newtonsoft.Json.dll",".\lib\ScriptHookVDotNet3.dll" tests\TestSpatialWorldModelArchitecture.cs
    echo Building TestSpatialOrchestrationPipeline.exe...
    C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:exe /out:TestSpatialOrchestrationPipeline.exe /r:".\dist\DynamicWorldEngine.dll",".\lib\Newtonsoft.Json.dll",".\lib\ScriptHookVDotNet3.dll" tests\TestSpatialOrchestrationPipeline.cs
    echo Building SyncInteriors.exe...
    C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:exe /out:SyncInteriors.exe /r:".\dist\DynamicWorldEngine.dll",".\lib\Newtonsoft.Json.dll",".\lib\ScriptHookVDotNet3.dll" tools\SyncInteriors.cs
    echo Build complete.
) else (
    echo.
    echo =========================================================
    echo   BUILD FAILED! Check error messages above.
    echo =========================================================
)
