using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Synapse.Core;
using Synapse.ActiveDirectory.Core;
using Synapse.Handlers.ActiveDirectory;

public class GroupMembershipHandler : HandlerRuntimeBase
{
    private GroupMembershipHandlerConfig _config;
    private bool _encounteredFailure = false;
    private int _sequenceNumber = 0;
    private string _context = "Execute";
    private string _mainProgressMsg = "";
    private readonly ExecuteResult _result = new ExecuteResult()
    {
        Status = StatusType.None,
        BranchStatus = StatusType.None,
        Sequence = 0
    };
    private readonly GroupMembershipResponse _response = new GroupMembershipResponse
    {
        Results = new List<Result>()
    };

    public override object GetConfigInstance()
    {
        return new GroupMembershipHandlerConfig()
        {
            DefaultDomain = "xxx"
        };
    }

    public override object GetParametersInstance()
    {
        return new GroupMembershipRequest()
        {
            AddSection = new List<AddSection>()
            {
                new AddSection()
                {
                    Domain = "xxxxxx",
                    Groups = new List<string>()
                    {
                        "xxxxxx"
                    },
                    Users = new List<string>()
                    {
                        "xxxxxx"
                    }
                },
                new AddSection()
                {
                    Domain = "xxxxxx",
                    Groups = new List<string>()
                    {
                        "xxxxxx"
                    },
                    Users = new List<string>()
                    {
                        "xxxxxx"
                    }
                },
                new AddSection()
                {
                    Domain = "xxxxxx",
                    Groups = new List<string>()
                    {
                        "xxxxxx"
                    },
                    Users = new List<string>()
                    {
                        "xxxxxx"
                    }
                }
            },
            DeleteSection = new List<DeleteSection>()
            {
                new DeleteSection()
                {
                    Domain = "xxxxxx",
                    Groups = new List<string>()
                    {
                        "xxxxxx",
                        "xxxxxx",
                        "xxxxxx",
                        "xxxxxx"
                    },
                    Users = new List<string>()
                    {
                        "xxxxxx"
                    }
                }
            }
        };
    }

    public override IHandlerRuntime Initialize(string values)
    {
        //deserialize the Config from the Handler declaration
        _config = DeserializeOrNew<GroupMembershipHandlerConfig>( values );
        return this;
    }

    public override ExecuteResult Execute(HandlerStartInfo startInfo)
    {
        string message;
        try
        {
            message = "Deserializing incoming request...";
            UpdateProgress( message, StatusType.Initializing );
            string inputParameters = RemoveParameterSingleQuote( startInfo.Parameters );
            GroupMembershipRequest parms = DeserializeOrNew<GroupMembershipRequest>( inputParameters );

            message = "Processing individual child request...";
            UpdateProgress( message, StatusType.Running );
            ProcessAddRequests( parms, startInfo.IsDryRun );
            ProcessDeleteRequests( parms, startInfo.IsDryRun );

            message = "Request has been processed" + (_encounteredFailure ? " with error found" : "") + ".";
            UpdateProgress( message, _encounteredFailure ? StatusType.CompletedWithErrors : StatusType.Success );

            _response.Status = message;
            message = "Serializing response...";
            UpdateProgress( message );
            _result.ExitData = JsonConvert.SerializeObject( _response );
        }
        catch ( Exception ex )
        {
            message = $"Execution has been aborted due to: {ex.Message}";
            UpdateProgress( message, StatusType.Failed );
        }

        message = startInfo.IsDryRun ? "Dry run execution is completed." : "Execution is completed.";
        UpdateProgress( message, StatusType.Any, true );
        return _result;
    }

    private void UpdateProgress(string message, StatusType status = StatusType.Any, bool isLastStep = false)
    {
        _mainProgressMsg = _mainProgressMsg + Environment.NewLine + message;
        if ( status != StatusType.Any )
        {
            _result.Status = status;
        }
        if ( isLastStep )
        {
            _sequenceNumber = int.MaxValue;
        }
        else
        {
            _sequenceNumber++;
        }
        OnProgress( _context, _mainProgressMsg, _result.Status, _sequenceNumber );
    }

    private void ProcessAddRequests(GroupMembershipRequest parms, bool isDryRun)
    {
        string message;
        int addSectionCount = 0;
        int addGroupCount = 0;
        int addUserCount = 0;
        if ( parms?.AddSection != null )
        {
            foreach ( AddSection addsection in parms.AddSection )
            {
                addSectionCount++;
                foreach ( string group in addsection.Groups )
                {
                    addGroupCount++;
                    foreach ( string user in addsection.Users )
                    {
                        addUserCount++;
                        try
                        {
                            message = $"Executing add request [{addSectionCount}/{addGroupCount}/{addUserCount}]"
                                + (isDryRun ? " in dry run mode..." : "...");
                            UpdateProgress( message );
                            DirectoryServices.AddUserToGroup( user, group, isDryRun, addsection.Domain );
                            Result r = new Result()
                            {
                                User = user,
                                Group = group,
                                Action = "add",
                                ExitCode = 0,
                                Note = isDryRun ? "Dry run has been completed." : "User has been successfully added to the group."
                            };
                            _response.Results.Add( r );
                            message = $"Processed add request [{addSectionCount}/{addGroupCount}/{addUserCount}].";
                            UpdateProgress( message );
                        }
                        catch ( Exception ex )
                        {
                            message = $"Encountered error while processing add request [{addSectionCount}/{addGroupCount}/{addUserCount}].";
                            UpdateProgress( message );
                            Result r = new Result()
                            {
                                User = user,
                                Group = group,
                                Action = "add",
                                ExitCode = -1,
                                Note = (isDryRun ? "Dry run has been completed. " : "") + ex.Message
                            };
                            _response.Results.Add( r );
                            _encounteredFailure = true;
                        }
                    }
                    addUserCount = 0;
                }
                addGroupCount = 0;
            }
        }
        else
        {
            message = "No add section is found from the incoming request.";
            UpdateProgress( message );
        }
    }

    private void ProcessDeleteRequests(GroupMembershipRequest parms, bool isDryRun)
    {
        string message;
        int deleteSectionCount = 0;
        int deleteGroupCount = 0;
        int deleteUserCount = 0;
        if ( parms?.DeleteSection != null )
        {
            foreach ( DeleteSection deleteSection in parms.DeleteSection )
            {
                deleteSectionCount++;
                foreach ( string group in deleteSection.Groups )
                {
                    deleteGroupCount++;
                    foreach ( string user in deleteSection.Users )
                    {
                        deleteUserCount++;
                        try
                        {
                            message = $"Executing delete request [{deleteSectionCount}/{deleteGroupCount}/{deleteUserCount}]"
                                               + (isDryRun ? " in dry run mode..." : "...");
                            UpdateProgress( message );
                            DirectoryServices.RemoveUserFromGroup( user, group, isDryRun, deleteSection.Domain );
                            Result r = new Result()
                            {
                                User = user,
                                Group = group,
                                Action = "delete",
                                ExitCode = 0,
                                Note = isDryRun ? "Dry run has been completed." : "User has been successfully removed from the group."
                            };
                            _response.Results.Add( r );
                            message = $"Processed delete request [{deleteSectionCount}/{deleteGroupCount}/{deleteUserCount}].";
                            UpdateProgress( message );
                        }
                        catch ( Exception ex )
                        {
                            message = $"Encountered error while processing delete request [{deleteSectionCount}/{deleteGroupCount}/{deleteUserCount}].";
                            UpdateProgress( message );
                            Result r = new Result()
                            {
                                User = user,
                                Group = group,
                                Action = "delete",
                                ExitCode = -1,
                                Note = (isDryRun ? "Dry run has been completed. " : "") + ex.Message
                            };
                            _response.Results.Add( r );
                            _encounteredFailure = true;
                        }
                    }
                    deleteUserCount = 0;
                }
                deleteGroupCount = 0;
            }
        }
        else
        {
            message = "No delete section is found from the incoming request.";
            UpdateProgress( message );
        }
    }

    private static string RemoveParameterSingleQuote(string input)
    {
        string output = "";
        if ( !string.IsNullOrWhiteSpace( input ) )
        {
            Regex pattern = new Regex( ":\\s*'" );
            output = pattern.Replace( input, ": " );
            pattern = new Regex( "'\\s*(\r\n|\r|\n|$)" );
            output = pattern.Replace( output, Environment.NewLine );
        }
        return output;
    }
}

