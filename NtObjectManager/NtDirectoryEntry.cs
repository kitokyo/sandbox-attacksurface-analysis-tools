﻿//  Copyright 2016 Google Inc. All Rights Reserved.
//
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//  http://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.

using NtApiDotNet;
using System;

namespace NtObjectManager
{
    /// <summary>
    /// A class representing a NT object manager directory entry.
    /// </summary>
    public class NtDirectoryEntry
    {
        private readonly NtDirectory _base_directory;
        private SecurityDescriptor _sd;
        private string _symlink_target;
        private Enum _maximum_granted_access;
        private bool _data_populated;

        private void PopulateData()
        {
            if (!_data_populated)
            {
                _data_populated = true;
                if (NtObject.CanOpenType(TypeName))
                {
                    try
                    {
                        using (var result = ToObject(false))
                        {
                            if (!result.IsSuccess)
                            {
                                return;
                            }
                            var obj = result.Result;
                            if (obj.IsAccessMaskGranted(GenericAccessRights.ReadControl))
                            {
                                _sd = obj.GetSecurityDescriptor(SecurityInformation.AllBasic, false).GetResultOrDefault();
                            }

                            if (obj is NtSymbolicLink link && link.IsAccessGranted(SymbolicLinkAccessRights.Query))
                            {
                                _symlink_target = link.GetTarget(false).GetResultOrDefault();
                            }

                            _maximum_granted_access = obj.GrantedAccessMask.ToSpecificAccess(obj.NtType.AccessRightsType);
                        }
                    }
                    catch
                    {
                    }
                }
            }
        }

        /// <summary>
        /// Get the name of the entry.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Get the NT type name of the entry.
        /// </summary>
        public string TypeName { get; }

        /// <summary>
        /// Indicates if this entry is a directory.
        /// </summary>
        public bool IsDirectory { get; }

        /// <summary>
        /// Indicates if this entry is a symbolic link.
        /// </summary>
        public bool IsSymbolicLink { get; }

        /// <summary>
        /// The relative path from the drive base to the entry.
        /// </summary>
        public string RelativePath { get; }

        /// <summary>
        /// The security descriptor of the entry. This can be null if caller does not have permission to open the actual object.
        /// </summary>
        public SecurityDescriptor SecurityDescriptor
        {
            get
            {
                PopulateData();
                return _sd;
            }
        }

        /// <summary>
        /// The symbolic link target if IsSymbolicLink is true. Can be null if caller doesn't have permission to open the actual object.
        /// </summary>
        public string SymbolicLinkTarget
        {
            get
            {
                PopulateData();
                return _symlink_target;
            }
        }

        /// <summary>
        /// The maximum granted access to the entry. Can be set to 0 if the caller doesn't have permission to open the actual object.
        /// </summary>
        public Enum MaximumGrantedAccess
        {
            get
            {
                PopulateData();
                return _maximum_granted_access;
            }
        }

        /// <summary>
        /// Try and open the directory entry and return an actual NtObject handle.
        /// </summary>
        /// <param name="throw_on_error">True to throw on error.</param>
        /// <returns>The object opened.</returns>
        /// <exception cref="System.ArgumentException">Thrown if invalid typename.</exception>
        public NtResult<NtObject> ToObject(bool throw_on_error)
        {
            return NtObject.OpenWithType(TypeName, RelativePath, _base_directory, 
                AttributeFlags.CaseInsensitive, GenericAccessRights.MaximumAllowed, null, throw_on_error);
        }

        /// <summary>
        /// Try and open the directory entry and return an actual NtObject handle.
        /// </summary>
        /// <returns>The object opened.</returns>
        /// <exception cref="NtException">Thrown if error opening object.</exception>
        /// <exception cref="System.ArgumentException">Thrown if invalid typename.</exception>
        public NtObject ToObject()
        {
            return ToObject(true).Result;
        }

        internal NtDirectoryEntry(NtDirectory base_directory, string relative_path, string name, string typename)
        {
            Name = name;
            TypeName = typename;
            RelativePath = relative_path;
            _base_directory = base_directory;

            switch (typename.ToLower())
            {
                case "directory":
                case "key":
                    IsDirectory = true;
                    break;
                case "symboliclink":
                    IsSymbolicLink = true;
                    break;
            }

            _maximum_granted_access = GenericAccessRights.None;
            _sd = new SecurityDescriptor();
        }

        /// <summary>
        /// Overridden ToString method.
        /// </summary>
        /// <returns>The name of the directory entry.</returns>
        public override string ToString()
        {
            return Name;
        }
    }
}
