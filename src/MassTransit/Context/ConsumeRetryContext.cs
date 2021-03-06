﻿// Copyright 2007-2016 Chris Patterson, Dru Sellers, Travis Smith, et. al.
//  
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use
// this file except in compliance with the License. You may obtain a copy of the 
// License at 
// 
//     http://www.apache.org/licenses/LICENSE-2.0 
// 
// Unless required by applicable law or agreed to in writing, software distributed
// under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR 
// CONDITIONS OF ANY KIND, either express or implied. See the License for the 
// specific language governing permissions and limitations under the License.
namespace MassTransit.Context
{
    public interface ConsumeRetryContext
    {
        /// <summary>
        /// The retry attempt in progress, or zero if this is the first time through
        /// </summary>
        int RetryAttempt { get; }

        /// <summary>
        /// The number of retries that have already been attempted, note that this is zero
        /// on the first retry attempt
        /// </summary>
        int RetryCount { get; }
    }
}