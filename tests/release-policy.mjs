import assert from 'node:assert/strict';
import {releaseMode} from '../scripts/Release-Policy.mjs';

const policy={schema:1,platform:'windows',firstReleaseApproved:false,automaticPublicReleasesAfterLaunch:true};
const initial={tagName:'v1.0.0',isDraft:false,isPrerelease:false,publishedAt:'2026-10-15T00:00:00Z'};
assert.equal(releaseMode(policy,'1.0.1',initial),'draft');
const enabled={...policy,firstReleaseApproved:true};
assert.equal(releaseMode(enabled,'1.0.1',null),'draft');
assert.equal(releaseMode(enabled,'1.0.1',{...initial,isDraft:true}),'draft');
assert.equal(releaseMode(enabled,'1.0.1',{...initial,isPrerelease:true}),'draft');
assert.equal(releaseMode(enabled,'1.0.0',initial),'draft');
assert.equal(releaseMode(enabled,'1.0.1-beta.1',initial),'draft');
assert.equal(releaseMode(enabled,'1.0.1',initial),'public');
assert.equal(releaseMode({...enabled,automaticPublicReleasesAfterLaunch:false},'1.0.1',initial),'draft');
assert.throws(()=>releaseMode({...enabled,firstReleaseApproved:'true'},'1.0.1',initial));
console.log('PASS release policy: initial approval, published first release, stable subsequent version and explicit switch are required');
