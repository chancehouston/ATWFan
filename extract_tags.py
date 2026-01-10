#!/usr/bin/env python3
"""
Extract tags from video titles and descriptions in the data files.
"""

import json
import re
from typing import List, Set, Dict

def extract_tags_from_text(title: str, description: str) -> List[str]:
    """Extract relevant tags from video title and description."""
    tags = set()

    # Combine title and description for analysis
    text = f"{title} {description}".lower()

    # Common keywords and phrases to extract as tags
    keywords = {
        # Locations
        'florida', 'orlando', 'disney', 'disneyland', 'universal', 'california',
        'las vegas', 'new york', 'hollywood', 'los angeles', 'miami', 'tokyo',
        'epcot', 'magic kingdom', 'hollywood studios', 'animal kingdom',
        'kissimmee', 'st cloud', 'san diego', 'japan', 'paris', 'london',

        # Types of places
        'abandoned', 'hotel', 'motel', 'restaurant', 'theme park', 'amusement park',
        'mall', 'shopping center', 'museum', 'attraction', 'resort', 'casino',
        'park', 'beach', 'airport', 'train station', 'ghost town',

        # Activities
        'exploring', 'vlog', 'daily vlog', 'tour', 'review', 'unboxing',
        'shopping', 'eating', 'dining', 'traveling', 'hiking', 'walking',
        'ride', 'roller coaster', 'adventure', 'urban exploration',

        # Specific attractions/brands
        'star wars', 'marvel', 'harry potter', 'transformers', 'jurassic',
        'walmart', 'target', 'mcdonalds', 'taco bell', 'arbys', 'wendys',
        'pizza hut', 'burger king', 'kfc', 'subway',

        # Events
        'christmas', 'halloween', 'thanksgiving', 'new year', 'birthday',
        'fourth of july', 'easter', 'valentine',

        # Content types
        'haunted', 'scary', 'creepy', 'mysterious', 'historic', 'vintage',
        'retro', 'nostalgic', 'behind the scenes', 'construction', 'demolition',
        'closed', 'opening day', 'grand opening', 'last day', 'final',

        # Transportation
        'road trip', 'van life', 'driving', 'flight', 'cruise', 'train',

        # Food
        'food', 'buffet', 'breakfast', 'lunch', 'dinner', 'snack', 'dessert',
    }

    # Extract keywords present in text
    for keyword in keywords:
        if keyword in text:
            # Normalize the tag
            tag = keyword.title().replace(' ', '')
            if tag not in ['A', 'An', 'The', 'In', 'On', 'At', 'To', 'From']:
                tags.add(tag)

    # Extract specific patterns

    # State abbreviations and full names
    states = {
        'FL': 'Florida', 'CA': 'California', 'NV': 'Nevada', 'NY': 'NewYork',
        'TX': 'Texas', 'AZ': 'Arizona', 'GA': 'Georgia', 'NC': 'NorthCarolina'
    }
    for abbr, full in states.items():
        if re.search(rf'\b{abbr}\b', title) or abbr.lower() in text:
            tags.add(full)

    # Check for "Daily Woo" pattern
    if 'daily woo' in text or 'the daily woo' in text:
        tags.add('DailyVlog')
        tags.add('DailyWoo')

    # Check for abandoned/exploration content
    if any(word in text for word in ['abandoned', 'exploring', 'exploration', 'sneaking', 'closed']):
        tags.add('UrbanExploration')

    # Check for theme park content
    if any(word in text for word in ['disney', 'universal', 'theme park', 'amusement', 'epcot', 'magic kingdom']):
        tags.add('ThemePark')

    # Check for food content
    if any(word in text for word in ['food', 'eating', 'restaurant', 'buffet', 'lunch', 'dinner', 'breakfast', 'menu']):
        tags.add('Food')

    # Check for travel content
    if any(word in text for word in ['travel', 'trip', 'vacation', 'journey', 'adventure']):
        tags.add('Travel')

    # Check for hotel/accommodation
    if any(word in text for word in ['hotel', 'motel', 'resort', 'room tour', 'staying at']):
        tags.add('Accommodation')

    # Extract year from title if present
    year_match = re.search(r'\b(20\d{2})\b', title)
    if year_match:
        tags.add(f'Year{year_match.group(1)}')

    # Extract "Day X" pattern from Daily Woo videos
    day_match = re.search(r'Day (\d+)', title)
    if day_match:
        tags.add('DailyVlog')

    # Return sorted list of tags
    return sorted(list(tags))

def process_video_file(filepath: str) -> Dict[str, List[str]]:
    """Process a video JSON file and extract tags for each video."""
    with open(filepath, 'r', encoding='utf-8') as f:
        data = json.load(f)

    video_tags = {}

    for video in data['Videos']:
        video_id = video['VideoId']
        title = video['Title']
        description = video['Description']

        tags = extract_tags_from_text(title, description)
        video_tags[video_id] = tags

    return video_tags

def main():
    """Main function to process all video files and create tags file."""
    print("Processing AdamTheWoo.json...")
    atw_tags = process_video_file('Data/AdamTheWoo.json')
    print(f"  Extracted tags for {len(atw_tags)} videos")

    print("Processing TheDailyWoo.json...")
    tdw_tags = process_video_file('Data/TheDailyWoo.json')
    print(f"  Extracted tags for {len(tdw_tags)} videos")

    # Combine all tags
    all_tags = {
        'AdamTheWoo': atw_tags,
        'TheDailyWoo': tdw_tags
    }

    # Create output structure
    output = {
        'GeneratedAt': '2026-01-10T00:00:00Z',
        'Description': 'Tags extracted from video titles and descriptions',
        'VideoTags': all_tags
    }

    # Write to new JSON file
    output_path = 'Data/VideoTags.json'
    with open(output_path, 'w', encoding='utf-8') as f:
        json.dump(output, f, indent=2, ensure_ascii=False)

    print(f"\nCreated {output_path}")
    print(f"Total videos tagged: {len(atw_tags) + len(tdw_tags)}")

    # Print some statistics
    all_unique_tags = set()
    for tags_dict in [atw_tags, tdw_tags]:
        for tags_list in tags_dict.values():
            all_unique_tags.update(tags_list)

    print(f"Total unique tags: {len(all_unique_tags)}")
    print(f"\nSample tags: {sorted(list(all_unique_tags))[:20]}")

if __name__ == '__main__':
    main()
