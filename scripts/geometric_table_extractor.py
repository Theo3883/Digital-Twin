#!/usr/bin/env python3
import json
import sys
from typing import List, Dict

AnalysisHeaders = ["ANALIZA", "TEST", "DENUMIRE", "ANALIZE"]
ResultHeaders = ["REZULTAT", "VALOARE"]
UnitHeaders = ["UNITATE", "U.M.", "UM"]
ReferenceHeaders = ["REFERINTA", "REFERINȚĂ", "REF", "VALORI"]
RowToleranceY = 0.012


def cluster_into_rows(tokens: List[Dict]) -> List[List[Dict]]:
    sorted_tokens = sorted(tokens, key=lambda t: (t['boundingBox']['y'] + t['boundingBox']['height']/2.0))
    rows: List[List[Dict]] = []
    current_row = None
    current_row_y = None
    for t in sorted_tokens:
        center_y = t['boundingBox']['y'] + t['boundingBox']['height']/2.0
        if current_row is None or abs(center_y - current_row_y) > RowToleranceY:
            current_row = []
            rows.append(current_row)
            current_row_y = center_y
        current_row.append(t)
    for row in rows:
        row.sort(key=lambda a: a['boundingBox']['x'])
    return rows


def find_header_row(rows: List[List[Dict]]) -> int:
    for i, row in enumerate(rows):
        row_text = " ".join([t['text'] for t in row]).upper()
        has_analysis = any(h in row_text for h in AnalysisHeaders)
        has_result = any(h in row_text for h in ResultHeaders)
        if has_analysis and has_result:
            return i
    return -1


def determine_columns(header_row: List[Dict]):
    analysis_range = result_range = unit_range = reference_range = None
    for token in header_row:
        upper = token['text'].upper()
        bb = token['boundingBox']
        start = int(bb['x'] * 10000)
        end = int((bb['x'] + bb['width']) * 10000)
        if any(h in upper for h in AnalysisHeaders):
            analysis_range = (start, end)
        elif any(h in upper for h in ResultHeaders):
            result_range = (start, end)
        elif any(h in upper for h in UnitHeaders):
            unit_range = (start, end)
        elif any(h in upper for h in ReferenceHeaders):
            reference_range = (start, end)
    return analysis_range, result_range, unit_range, reference_range


def parse_data_row(row: List[Dict], columns):
    analysis_name = result_value = unit = reference = None
    for token in row:
        center_x = int((token['boundingBox']['x'] + token['boundingBox']['width']/2.0) * 10000)
        if columns[0] and columns[0][0] <= center_x <= columns[0][1]:
            analysis_name = (analysis_name + ' ' + token['text']) if analysis_name else token['text']
        elif columns[1] and columns[1][0] <= center_x <= columns[1][1]:
            result_value = (result_value + ' ' + token['text']) if result_value else token['text']
        elif columns[2] and columns[2][0] <= center_x <= columns[2][1]:
            unit = (unit + ' ' + token['text']) if unit else token['text']
        elif columns[3] and columns[3][0] <= center_x <= columns[3][1]:
            reference = (reference + ' ' + token['text']) if reference else token['text']
    if not analysis_name or not result_value:
        return None
    return {'analysis': analysis_name.strip(), 'value': result_value.strip(), 'unit': unit.strip() if unit else None, 'reference': reference.strip() if reference else None}


def extract(graph: Dict):
    pages = graph.get('pages', [])
    # use first page's tokens
    tokens = graph.get('allTokens', [])
    if not tokens:
        return []
    rows = cluster_into_rows(tokens)
    header_idx = find_header_row(rows)
    if header_idx < 0:
        return []
    columns = determine_columns(rows[header_idx])
    if columns[0] is None or columns[1] is None:
        return []
    lab_results = []
    for i in range(header_idx+1, len(rows)):
        r = parse_data_row(rows[i], columns)
        if r:
            lab_results.append(r)
    return lab_results


if __name__ == '__main__':
    if len(sys.argv) < 2:
        print('Usage: geometric_table_extractor.py <native_debug_in.json>')
        sys.exit(1)
    inpath = sys.argv[1]
    j = json.load(open(inpath, 'r', encoding='utf-8'))
    graph = j.get('graph')
    if not graph:
        print('No graph found in input JSON')
        sys.exit(2)
    out = extract(graph)
    print(json.dumps(out, ensure_ascii=False, indent=2))
