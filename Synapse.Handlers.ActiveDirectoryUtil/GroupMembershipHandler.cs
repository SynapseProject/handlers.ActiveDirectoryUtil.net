using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Synapse.Core;
using Synapse.ActiveDirectory.Core;

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
            ValidDomains = new List<string>()
            {
                "xxx",
                "yyy",
                "zzz"
            }
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
                    Domain = "yyyyyy",
                    Groups = new List<string>()
                    {
                        "yyyyyy"
                    },
                    Users = new List<string>()
                    {
                        "yyyyyy"
                    }
                }
            },
            DeleteSection = new List<DeleteSection>()
            {
                new DeleteSection()
                {
                    Domain = "zzzzzz",
                    Groups = new List<string>()
                    {
                        "zzzzzz",
                        "zzzzzz"
                    },
                    Users = new List<string>()
                    {
                        "zzzzzz"
                    }
                }
            }
        };
    }

    public override IHandlerRuntime Initialize(string values)
    {
        try
        {
            _config = DeserializeOrNew<GroupMembershipHandlerConfig>( values );

            if ( _config?.ValidDomains != null && _config.ValidDomains.Count > 0 )
            {
                _config.ValidDomains = _config.ValidDomains.ConvertAll( d => d.ToLower() );
            }
            else
            {
                _config = new GroupMembershipHandlerConfig { ValidDomains = new List<string>() };
            }
        }
        catch ( Exception ex )
        {
            OnLogMessage( "Initialization", "Encountered exception while deserializing handler config.", LogLevel.Error, ex );
        }

        return this;
    }

    public override ExecuteResult Execute(HandlerStartInfo startInfo)
    {
        string message;
        try
        {
            message = "Deserializing incoming requests...";
            UpdateProgress( message, StatusType.Initializing );
            GroupMembershipRequest parms = DeserializeOrNew<GroupMembershipRequest>( startInfo.Parameters );

            message = "Processing individual child request...";
            UpdateProgress( message, StatusType.Running );
            ProcessAddRequests( parms, startInfo.IsDryRun );
            ProcessDeleteRequests( parms, startInfo.IsDryRun );

            message = (startInfo.IsDryRun ? "Dry run execution is completed" : "Execution is completed") 
                + (_encounteredFailure ? " with errors encountered" : "") + ".";
            UpdateProgress( message, _encounteredFailure ? StatusType.CompletedWithErrors : StatusType.Success );
        }
        catch ( Exception ex )
        {
            message = $"Execution has been aborted due to: {ex.Message}";
            UpdateProgress( message, StatusType.Failed );
            _encounteredFailure = true;
        }
        _response.Status = message;

        message = "Serializing response...";
        UpdateProgress( message, StatusType.Any, true );
        _result.ExitData = JsonConvert.SerializeObject( _response );
        _result.ExitCode = _encounteredFailure ? -1 : 0;

        return _result;
    }

    private void UpdateProgress(string message, StatusType status = StatusType.Any, bool isLastStep = false)
    {
        _mainProgressMsg = message;
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
        OnLogMessage( _context, _mainProgressMsg);
    }

    private void ProcessAddRequests(GroupMembershipRequest parms, bool isDryRun)
    {
        string message;
        int addSectionCount = 0;
        int addGroupCount = 0;
        int addUserCount = 0;
        if ( parms?.AddSection != null )
        {
            foreach ( AddSection addSection in parms.AddSection )
            {
                addSectionCount++;
                foreach ( string group in addSection.Groups )
                {
                    addGroupCount++;
                    foreach ( string user in addSection.Users )
                    {
                        addUserCount++;
                        message = $"Executing add request [{addSectionCount}/{addGroupCount}/{addUserCount}]"
                            + (isDryRun ? " in dry run mode..." : "...");
                        UpdateProgress( message );
                        OnLogMessage( _context, $"Domain: {addSection.Domain}, Group: {group}, User: {user}" );
                        try
                        {
                            if ( !IsValidDomain( addSection.Domain ) )
                            {
                                throw new Exception( "Domain specified is not valid." );
                            }
                            DirectoryServices.AddUserToGroup( user, group, isDryRun, addSection.Domain );
                            Result r = new Result()
                            {
                                Domain = addSection.Domain,
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
                            Result r = new Result()
                            {
                                Domain = addSection.Domain,
                                User = user,
                                Group = group,
                                Action = "add",
                                ExitCode = -1,
                                Note = ex.Message
                            };
                            _response.Results.Add( r );
                            _encounteredFailure = true;
                            message = $"Encountered error while processing add request [{addSectionCount}/{addGroupCount}/{addUserCount}].";
                            UpdateProgress( message );
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
                        message = $"Executing delete request [{deleteSectionCount}/{deleteGroupCount}/{deleteUserCount}]"
                                           + (isDryRun ? " in dry run mode..." : "...");
                        UpdateProgress( message );
                        OnLogMessage( _context, $"Domain: {deleteSection.Domain}, Group: {group}, User: {user}" );
                        try
                        {
                            if ( !IsValidDomain( deleteSection.Domain ) )
                            {
                                throw new Exception( "Domain specified is not valid." );
                            }
                            DirectoryServices.RemoveUserFromGroup( user, group, isDryRun, deleteSection.Domain );
                            Result r = new Result()
                            {
                                Domain = deleteSection.Domain,
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
                            Result r = new Result()
                            {
                                Domain = deleteSection.Domain,
                                User = user,
                                Group = group,
                                Action = "delete",
                                ExitCode = -1,
                                Note = ex.Message
                            };
                            _response.Results.Add( r );
                            _encounteredFailure = true;
                            message = $"Encountered error while processing delete request [{deleteSectionCount}/{deleteGroupCount}/{deleteUserCount}].";
                            UpdateProgress( message );
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


    private bool IsValidDomain(string domain)
    {
        bool isValid;

        if ( _config?.ValidDomains == null || _config.ValidDomains.Count == 0 )
        {
            // Domain passed in considered valid if there is no pre-defined valid domains in config.
            isValid = true;
        }
        else if ( String.IsNullOrWhiteSpace( domain ) )
        {
            // Empty domain is a valid equivalent to default domain.
            isValid = true;
        }
        else
        {
            // Check if domain passed in is among the pre-defined values in the config.
            isValid = _config.ValidDomains.Contains( domain.ToLower() );
        }

        return isValid;
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

