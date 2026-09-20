\# WonClick Project Context



\## 1. Project



\- Project Name: WonClick

\- Product: Autodesk Revit Add-in

\- Revit Version: Revit 2025

\- Framework: .NET 8.0-windows

\- Platform: x64

\- Language: C#

\- UI: WPF



\---



\## 2. Development Principles



\### Critical Rules



1\. Existing working features must not be broken when adding new features.



2\. Parameter Export / Import and AutoParameter are independent features.



3\. Do not modify the existing Parameter feature unless explicitly required.



4\. When modifying a source file, provide the full source file rather than code fragments.



5\. Common files such as WonClickApplication.cs must preserve all existing functionality.



6\. New features should preferably be isolated in their own folders / namespaces.



7\. The latest stable Git main branch is the source of truth.



\---



\## 3. Existing Feature - Parameter



The existing Parameter feature manages Revit parameters through Export / Import.



Entry Command:



WonClick.Commands.ParameterCommand



Important:



\- Existing completed feature.

\- Primarily works from the current 3D View.

\- Must not be modified by AutoParameter development unless explicitly required.



\---



\## 4. Existing Feature - AutoParameter



Purpose:



Automatically populate selected Instance Parameters when new model elements are created.



\### Architecture



\- IUpdater based

\- Element.GetChangeTypeElementAddition()

\- Modeless WPF Window

\- ExternalEvent used for modeless Revit API interaction

\- AutoParameterEngine manages rules by Document



\### Parameter Scope



Only:



\- Instance Parameters

\- Model Category elements

\- Parameter Group = Text or Set

\- ReadOnly = false



Do not retrieve Type Parameters for AutoParameter rules.



\### Rules



Multiple AutoParameter rules can be active simultaneously.



Each rule contains:



\- Enabled

\- ParameterId

\- ParameterName

\- SharedParameterGuid if available

\- Value



If the newly created element does not contain a selected parameter, the parameter is skipped.



\---



\## 5. AutoParameter UI



The AutoParameter Window supports:



\- Multiple rules

\- Add Rule

\- Remove Rule

\- Use checkbox

\- Parameter selection

\- Value input

\- Apply / Update

\- OFF

\- Modeless operation

\- Export Settings

\- Import Settings

\- Automatic Hold of the last UI settings



Closing the window does not automatically disable an active AutoParameter rule.



\---



\## 6. AutoParameter Presets



Preset format:



.wcap / JSON



Features:



\- Export current AutoParameter rules

\- Import saved rules

\- Shared Parameter GUID is preferred for matching

\- Parameter Name used as fallback

\- ParameterId used as an additional fallback



Automatic Hold file stores the most recent UI settings across Revit restarts.



\---



\## 7. AutoParameter Catalog Performance



The Parameter selection catalog is optimized.



Architecture:



AutoParameterWindow

&#x20;   ->

ExternalEvent

&#x20;   ->

AutoParameterCatalogService

&#x20;   ->

Document-specific cache



Goals:



\- Window appears before expensive parameter scanning.

\- Do not scan every parameter of every element repeatedly.

\- Representative Model Instances are used for catalog construction.

\- Catalog is cached per Document.

\- DocumentChanged is used read-only to mark the catalog cache dirty when relevant structure changes.

\- Normal creation of an already-known instance/type should not unnecessarily invalidate the catalog.



Do not use Task.Run to access the Revit API.



Revit API access must remain on a valid Revit API context.



\---



\## 8. AutoParameter Relevant Structure



AutoParameter/

&#x20;   Models/

&#x20;       AutoParameterRule.cs

&#x20;       AutoParameterRuleRow.cs

&#x20;       InstanceParameterItem.cs

&#x20;       AutoParameterPreset.cs



&#x20;   Services/

&#x20;       AutoParameterEngine.cs

&#x20;       AutoParameterCatalogService.cs

&#x20;       AutoParameterPresetService.cs



&#x20;   Updaters/

&#x20;       AutoParameterUpdater.cs



&#x20;   Commands/

&#x20;       AutoParameterCommand.cs



&#x20;   ExternalEvents/

&#x20;       AutoParameterExternalEventHandler.cs



&#x20;   UI/

&#x20;       AutoParameterWindow.xaml

&#x20;       AutoParameterWindow.xaml.cs



\---



\## 9. Namespaces



Main namespace:



WonClick



Important AutoParameter namespaces:



WonClick.AutoParameter.Models

WonClick.AutoParameter.Services

WonClick.AutoParameter.Updaters

WonClick.AutoParameter.Commands

WonClick.AutoParameter.ExternalEvents

WonClick.AutoParameter.UI



The project also contains a WonClick.Parameter namespace.



When Autodesk.Revit.DB.Parameter causes a naming conflict, use:



using RevitParameter = Autodesk.Revit.DB.Parameter;



\---



\## 10. Licensing



Licensing has been discussed but is currently postponed.



Do not enable or gate WonClick functionality through licensing unless explicitly requested.



\---



\## 11. Current Development State



Current development focus:



AutoParameter performance and usability.



Current expected features:



\- Existing Parameter Export / Import works.

\- AutoParameter works.

\- Multiple rules work.

\- Text / Set parameter-group filtering works.

\- ReadOnly parameters are excluded.

\- Hold settings work.

\- AutoParameter Export / Import works.

\- Parameter Catalog caching / loading optimization is being implemented and tested.



\---



\## 12. AI Development Workflow



When starting development from a new ChatGPT conversation:



1\. Read this PROJECT\_CONTEXT.md first.

2\. Inspect the latest main branch.

3\. Inspect the files involved in the requested feature.

4\. Preserve all existing functionality.

5\. Do not recreate files from old conversation memory when newer repository versions exist.

6\. Treat repository main as authoritative.

7\. Provide complete modified files when code changes are requested.

